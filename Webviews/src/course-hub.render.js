window.updateCourses = function (courses) {
    allCourses = courses || [];
    if (selectedSet) {
        const updated = allCourses.find(course => course.id === selectedSet.id);
        if (updated) {
            selectedSet = {
                id: updated.id,
                title: updated.title,
                count: updated.count,
                language: updated.language || "",
                languageCode: updated.languageCode || ""
            };
        } else {
            selectedSet = null;
        }
    }
    if (currentView === "topics") {
        sendToBackend("getTopics"); // request fresh topics count
    } else {
        renderCourses(allCourses);
    }
};

window.updateTopics = function (topics) {
    allTopics = topics || [];
    if (currentView === "topics") {
        renderTopics(allTopics);
    } else {
        // Find if our selected topic was updated or deleted
        const updated = allTopics.find(t => t.id === selectedTopic.id);
        if (updated) {
            selectedTopic = updated;
            document.getElementById("dashboardTitle").textContent = selectedTopic.title;
        }
        renderCourses(allCourses);
    }
};

window.enterTopic = function (topic) {
    currentView = "courses";
    selectedTopic = topic;
    
    const btnBack = document.getElementById("btnBackToTopics");
    if (btnBack) btnBack.style.display = "none";
    document.getElementById("languageFilterWrap").style.display = "block";
    document.getElementById("dueFilterWrap").style.display = "block";
    document.getElementById("searchInput").placeholder = "Tìm học phần...";
    document.getElementById("searchInput").value = ""; // clear search
    
    if (window.rebuildSortDropdown) window.rebuildSortDropdown();
    renderCourses(allCourses);
};

window.goBackToTopics = function () {
    currentView = "topics";
    selectedTopic = null;
    
    const btnBack = document.getElementById("btnBackToTopics");
    if (btnBack) btnBack.style.display = "none";
    document.getElementById("languageFilterWrap").style.display = "none";
    document.getElementById("dueFilterWrap").style.display = "none";
    document.getElementById("searchInput").placeholder = "Tìm chủ đề...";
    document.getElementById("searchInput").value = ""; // clear search
    
    if (window.rebuildSortDropdown) window.rebuildSortDropdown();
    sendToBackend("getTopics");
};

function renderTopics(topics) {
    const grid = document.getElementById("courseGrid");
    const info = document.getElementById("searchInfo");
    if (!grid || !info) return;

    grid.innerHTML = "";
    
    // Client-side search filter for topics
    const query = (document.getElementById("searchInput").value || "").trim().toLowerCase();
    let filteredTopics = topics || [];
    if (query) {
        filteredTopics = filteredTopics.filter(t => (t.title || "").toLowerCase().includes(query));
    }
    
    const view = applyTopicSort(filteredTopics);

    // Prepend the "+" Create Topic card
    const createCard = document.createElement("div");
    createCard.className = "course course-card course-create-card";
    createCard.innerHTML = `
        <div class="create-card-content">
            <div class="create-card-icon">+</div>
            <div class="create-card-text">Tạo chủ đề</div>
        </div>
    `;
    createCard.onclick = () => window.requestCreateTopic();
    grid.appendChild(createCard);

    view.forEach(topic => {
        const tile = document.createElement("div");
        tile.className = "course course-card topic-card";
        const coverImageUrl = topic.coverImageUrl || "https://app/Webviews/icon/bg-card.png";
        tile.classList.add("has-cover");
        tile.style.setProperty("--course-cover-image", `url("${String(coverImageUrl).replaceAll('"', '%22')}")`);

        const isDefault = topic.id === "default_topic";
        const actionsHtml = isDefault ? "" : `
            <div class="card-actions">
                <button class="card-action-btn edit-btn" title="Sửa">Sửa</button>
                <button class="card-action-btn delete-btn" title="Xóa">Xóa</button>
            </div>
        `;

        tile.innerHTML = `
            <div class="card-header">
                ${actionsHtml}
            </div>
            <div class="topic-card-content">
                <div class="topic-card-title" title="${escapeHtml(topic.title)}">${escapeHtml(topic.title)}</div>
                <div class="topic-card-count">${topic.count} học phần</div>
            </div>
        `;

        if (!isDefault) {
            const editBtn = tile.querySelector(".edit-btn");
            if (editBtn) {
                editBtn.onclick = ev => {
                    ev.stopPropagation();
                    window.requestEditTopic(topic);
                };
            }

            const deleteBtn = tile.querySelector(".delete-btn");
            if (deleteBtn) {
                deleteBtn.onclick = ev => {
                    ev.stopPropagation();
                    window.requestDeleteTopic(topic);
                };
            }
        }

        tile.onclick = () => {
            document.querySelectorAll(".course.course-card").forEach(c => c.classList.remove("selected"));
            tile.classList.add("selected");
        };

        tile.ondblclick = () => {
            window.enterTopic(topic);
        };

        grid.appendChild(tile);
    });

    info.textContent = `Có ${filteredTopics.length} chủ đề`;
    document.getElementById("dashboardTitle").textContent = "Danh sách chủ đề";
}

function applyTopicSort(list) {
    if (!Array.isArray(list) || list.length <= 1) return list;
    const getTitle = x => (x && x.title != null ? String(x.title) : "");
    const getCount = x => (x && x.count != null ? Number(x.count) : 0);
    const getId = x => (x && x.id != null ? String(x.id) : "");

    switch (sortMode) {
        case "title_asc":
            list.sort((a, b) => {
                const c = __courseCollator.compare(getTitle(a), getTitle(b));
                return c !== 0 ? c : __courseCollator.compare(getId(a), getId(b));
            });
            break;
        case "title_desc":
            list.sort((a, b) => {
                const c = __courseCollator.compare(getTitle(b), getTitle(a));
                return c !== 0 ? c : __courseCollator.compare(getId(a), getId(b));
            });
            break;
        case "count_asc":
            list.sort((a, b) => getCount(a) - getCount(b));
            break;
        case "count_desc":
            list.sort((a, b) => getCount(b) - getCount(a));
            break;
        default:
            break;
    }
    return list;
}

function renderCourses(courses) {
    if (currentView === "topics") {
        renderTopics(allTopics);
        return;
    }

    const grid = document.getElementById("courseGrid");
    const info = document.getElementById("searchInfo");
    if (!grid || !info) return;

    grid.innerHTML = "";
    
    // Filter courses by the selected topic
    const topicId = selectedTopic ? selectedTopic.id : "default_topic";
    const topicFiltered = (courses || []).filter(course => {
        if (topicId === "default_topic") {
            const topicIds = allTopics.map(t => t.id);
            return !course.topicId || !topicIds.includes(course.topicId) || course.topicId === "default_topic";
        } else {
            return course.topicId === topicId;
        }
    });

    const source = topicFiltered.slice();
    rebuildLanguageFilter(source);
    const view = applySort(applyFilters(source));

    // Prepend the "← Quay lại" card
    const backCard = document.createElement("div");
    backCard.className = "course course-card course-create-card course-back-card";
    backCard.innerHTML = `
        <div class="create-card-content">
            <div class="create-card-icon">←</div>
            <div class="create-card-text">Quay lại</div>
        </div>
    `;
    backCard.onclick = () => window.goBackToTopics();
    grid.appendChild(backCard);

    // Prepend the "+" Create Course card
    const createCard = document.createElement("div");
    createCard.className = "course course-card course-create-card";
    createCard.innerHTML = `
        <div class="create-card-content">
            <div class="create-card-icon">+</div>
            <div class="create-card-text">Tạo học phần</div>
        </div>
    `;
    createCard.onclick = () => window.createCourse();
    grid.appendChild(createCard);

    view.forEach(course => {
        const tile = document.createElement("div");
        tile.className = "course course-card";
        const coverImageUrl = course.coverImageUrl || "https://app/Webviews/icon/bg-card.png";
        tile.classList.add("has-cover");
        tile.style.setProperty("--course-cover-image", `url("${String(coverImageUrl).replaceAll('"', '%22')}")`);

        tile.innerHTML = `
            <div class="card-header">
                <span class="card-title" title="${escapeHtml(course.title)}">${escapeHtml(course.title)}</span>
                <div class="card-actions">
                    <button class="card-action-btn edit-btn" title="Sửa">Sửa</button>
                    <button class="card-action-btn delete-btn" title="Xóa">Xóa</button>
                </div>
            </div>
            <div class="card-body">
                <div class="card-meta">
                    <svg class="meta-icon" viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                        <path d="M4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6zm16-4H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H8V4h12v12z"/>
                    </svg>
                    <span class="card-count">${Number(course.unlearnedCount ?? course.count ?? 0)} thẻ chưa thuộc</span>
                </div>
            </div>
        `;

        const editBtn = tile.querySelector(".edit-btn");
        editBtn.onclick = ev => {
            ev.stopPropagation();
            requestEditCourse(course);
        };

        const deleteBtn = tile.querySelector(".delete-btn");
        deleteBtn.onclick = ev => {
            ev.stopPropagation();
            requestDelete(course);
        };

        tile.onclick = () => selectCourse(course, tile);
        tile.ondblclick = () => {
            selectCourse(course, tile);
            window.showFeatureWithCheck("flashcards");
        };

        if (selectedSet && selectedSet.id === course.id) {
            tile.classList.add("selected");
        }

        grid.appendChild(tile);
    });

    info.textContent = `Có ${view.length} học phần`;
    if (selectedTopic) {
        document.getElementById("dashboardTitle").textContent = selectedTopic.title;
    }

    refreshHeroText();
}

function applySort(list) {
    if (!Array.isArray(list) || list.length <= 1) return list;

    const getTitle = x => (x && x.title != null ? String(x.title) : "");
    const getCount = x => {
        const n = x && x.count != null ? Number(x.count) : 0;
        return Number.isFinite(n) ? n : 0;
    };
    const getId = x => (x && x.id != null ? String(x.id) : "");

    switch (sortMode) {
        case "title_asc":
            list.sort((a, b) => {
                const c = __courseCollator.compare(getTitle(a), getTitle(b));
                return c !== 0 ? c : __courseCollator.compare(getId(a), getId(b));
            });
            break;

        case "title_desc":
            list.sort((a, b) => {
                const c = __courseCollator.compare(getTitle(b), getTitle(a));
                return c !== 0 ? c : __courseCollator.compare(getId(a), getId(b));
            });
            break;

        case "count_asc":
            list.sort((a, b) => {
                const c = getCount(a) - getCount(b);
                return c !== 0 ? c : __courseCollator.compare(getTitle(a), getTitle(b));
            });
            break;

        case "count_desc":
            list.sort((a, b) => {
                const c = getCount(b) - getCount(a);
                return c !== 0 ? c : __courseCollator.compare(getTitle(a), getTitle(b));
            });
            break;

        default:
            break;
    }

    return list;
}

function applyFilters(list) {
    if (!Array.isArray(list)) return [];
    let result = list;
    if (languageFilter && languageFilter !== "all") {
        result = result.filter(course => getCourseLanguageKey(course) === languageFilter);
    }
    const dueCheckbox = document.getElementById("dueFilterCheckbox");
    if (dueCheckbox && dueCheckbox.checked) {
        result = result.filter(course => Number(course.dueCount || 0) > 0);
    }
    return result;
}

function rebuildLanguageFilter(courses) {
    const select = document.getElementById("languageFilterSelect");
    const wrap = document.getElementById("languageSelectWrap");
    const btn = document.getElementById("languageSelectBtn");
    const list = document.getElementById("languageSelectList");
    if (!select) return;

    const current = languageFilter || select.value || "all";
    const languages = new Map();

    (courses || []).forEach(course => {
        const key = getCourseLanguageKey(course);
        if (!key || languages.has(key)) return;
        languages.set(key, getCourseLanguageLabel(course, key));
    });

    const options = ['<option value="all">Tất cả ngôn ngữ</option>'];
    Array.from(languages.entries())
        .sort((a, b) => __courseCollator.compare(a[1], b[1]))
        .forEach(([key, label]) => {
            options.push(`<option value="${escapeHtml(key)}">${escapeHtml(label)}</option>`);
        });

    select.innerHTML = options.join("");

    languageFilter = current !== "all" && languages.has(current) ? current : "all";
    select.value = languageFilter;
    const selectedOption = select.options[select.selectedIndex];
    if (selectedOption) {
        select.title = selectedOption.textContent || "";
        select.setAttribute("aria-label", selectedOption.textContent || "Language filter");
        if (btn) {
            btn.title = selectedOption.textContent || "";
            btn.setAttribute("aria-label", selectedOption.textContent || "Language filter");
        }
    }

    if (list && wrap && btn) {
        list.innerHTML = "";
        Array.from(select.options).forEach(option => {
            const div = document.createElement("div");
            div.className = "language-option";
            if (option.value === languageFilter) div.classList.add("active");
            div.textContent = option.textContent || "";
            div.onclick = () => {
                languageFilter = option.value || "all";
                select.value = languageFilter;
                select.title = option.textContent || "";
                select.setAttribute("aria-label", option.textContent || "Language filter");
                btn.title = option.textContent || "";
                btn.setAttribute("aria-label", option.textContent || "Language filter");
                wrap.classList.remove("open");
                renderCourses(allCourses);
            };
            list.appendChild(div);
        });
    }
}

function getCourseLanguageKey(course) {
    const raw = (course && (course.languageCode || course.language)) || "";
    const value = String(raw).trim();
    if (!value) return "";

    const lower = value.toLowerCase();
    if (lower === "zh" || lower === "zh-tw" || lower === "zh-hant" || lower === "zh-hk" || lower === "zh-mo") return "zh-TW";
    if (lower === "zh-cn" || lower === "zh-hans" || lower === "zh-sg") return "zh-CN";

    return lower.length <= 3 ? lower : value;
}

function getCourseLanguageLabel(course, key) {
    const language = course && course.language ? String(course.language).trim() : "";
    if (language) return language;

    switch (key) {
        case "zh-TW": return "Tiếng Trung phồn thể";
        case "zh-CN": return "Tiếng Trung giản thể";
        case "en": return "Tiếng Anh";
        case "vi": return "Tiếng Việt";
        case "ja": return "Tiếng Nhật";
        case "ko": return "Tiếng Hàn";
        case "de": return "Tiếng Đức";
        case "fr": return "Tiếng Pháp";
        case "es": return "Tiếng Tây Ban Nha";
        case "ru": return "Tiếng Nga";
        default: return key || "Ngôn ngữ khác";
    }
}

function selectCourse(course, tile) {
    document.querySelectorAll(".course").forEach(t =>
        t.classList.remove("selected")
    );

    tile.classList.add("selected");

    selectedSet = {
        id: course.id,
        title: course.title,
        count: course.count,
        language: course.language || "",
        languageCode: course.languageCode || ""
    };

    sendToBackend("selectCourse", { id: course.id });
    updateSelectedUI();
    refreshHeroText();
}

function updateSelectedUI() {
    const chip = document.getElementById("selectedChip");
    const subLine = document.getElementById("subLine");
    if (!chip || !subLine) return;

    if (!selectedSet) {
        chip.style.display = "none";
        subLine.textContent = "Chọn học phần để bắt đầu";
        return;
    }

    chip.style.display = "inline-flex";
    document.getElementById("selectedTitle").textContent = selectedSet.title;
    document.getElementById("selectedCount").textContent = selectedSet.count;
    subLine.textContent = "Chọn tính năng để học";
}

function refreshHeroText() {
    const ht = document.getElementById("heroTitle");
    const hs = document.getElementById("heroSub");
    if (!ht || !hs) return;

    if (!selectedSet) {
        hs.innerHTML =
            `Chọn một học phần bên trái, sau đó bấm <b>Học thẻ</b>` +
            ` hoặc <b>Kiểm tra</b>.<br>` +
            `Double click vào học phần để vào học thẻ nhanh.`;
        updateSelectedUI();
        return;
    }

    ht.textContent = selectedSet.title;
    hs.innerHTML =
        `Học phần đang chọn có <b>${selectedSet.count}</b> thẻ. ` +
        `Bạn có thể bắt đầu học hoặc làm kiểm tra ngay.`;
    updateSelectedUI();
}

function escapeHtml(str) {
    if (!str) return "";
    return String(str)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

window.rebuildSortDropdown = function () {
    const wrap = document.getElementById("sortSelectWrap");
    const btn = document.getElementById("sortSelectBtn");
    const list = document.getElementById("sortSelectList");

    if (!wrap || !btn || !list) return;

    const isTopics = currentView === "topics";
    const options = [
        { value: "default", label: "Mặc định" },
        { value: "title_asc", label: "Tên A → Z" },
        { value: "title_desc", label: "Tên Z → A" },
        { value: "count_asc", label: isTopics ? "Học phần tăng dần" : "Số thẻ tăng dần" },
        { value: "count_desc", label: isTopics ? "Học phần giảm dần" : "Số thẻ giảm dần" }
    ];

    const setButtonLabel = label => {
        btn.innerHTML = `<i class="fa-solid fa-filter" aria-hidden="true"></i> ${label} <span class="caret">▾</span>`;
        btn.title = label;
        btn.setAttribute("aria-label", label);
    };

    list.innerHTML = "";
    options.forEach(opt => {
        const div = document.createElement("div");
        div.className = "sort-option";
        if (opt.value === sortMode) div.classList.add("active");
        div.textContent = opt.label;

        div.onclick = () => {
            sortMode = opt.value;
            setButtonLabel(opt.label);
            wrap.classList.remove("open");

            list.querySelectorAll(".sort-option")
                .forEach(o => o.classList.remove("active"));
            div.classList.add("active");

            if (currentView === "topics") {
                renderTopics(allTopics);
            } else {
                renderCourses(allCourses);
            }
        };

        list.appendChild(div);
    });
    setButtonLabel((options.find(opt => opt.value === sortMode) || options[0]).label);
};

(() => {
    const wrap = document.getElementById("sortSelectWrap");
    const btn = document.getElementById("sortSelectBtn");
    if (!wrap || !btn) return;

    window.rebuildSortDropdown();

    btn.onclick = e => {
        e.stopPropagation();
        wrap.classList.toggle("open");
    };

    document.addEventListener("click", () => {
        wrap.classList.remove("open");
    });
})();

(() => {
    const wrap = document.getElementById("languageSelectWrap");
    const btn = document.getElementById("languageSelectBtn");
    if (!wrap || !btn) return;

    btn.onclick = e => {
        e.stopPropagation();
        wrap.classList.toggle("open");
    };

    document.addEventListener("click", () => {
        wrap.classList.remove("open");
    });
})();
