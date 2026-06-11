let pendingEditCourse = null;

const COURSE_LANGUAGE_OPTIONS = [
    { code: "en", label: "Tiếng Anh (English)" },
    { code: "zh-TW", label: "Tiếng Trung phồn thể (TOCFL / Taiwan)" },
    { code: "zh-CN", label: "Tiếng Trung giản thể (Mainland)" },
    { code: "vi", label: "Tiếng Việt (Vietnamese)" },
    { code: "ja", label: "Tiếng Nhật (Japanese)" },
    { code: "ko", label: "Tiếng Hàn (Korean)" },
    { code: "de", label: "Tiếng Đức (German)" },
    { code: "fr", label: "Tiếng Pháp (French)" },
    { code: "es", label: "Tiếng Tây Ban Nha (Spanish)" },
    { code: "ru", label: "Tiếng Nga (Russian)" }
];

function requestDelete(course) {
    pendingDelete = course;
    pendingDeleteType = "course";
    document.getElementById("confirmTitle").textContent = "Xóa học phần";
    document.getElementById("confirmBody").innerHTML =
        `Bạn có chắc chắn muốn xóa học phần:<br>` +
        `<b>${escapeHtml(course.title)}</b>?<br>` +
        `<span style="color:var(--muted)">Hành động này không thể hoàn tác.</span>`;
    openConfirm();
}

window.requestDeleteTopic = function (topic) {
    pendingDelete = topic;
    pendingDeleteType = "topic";
    document.getElementById("confirmTitle").textContent = "Xóa chủ đề";
    document.getElementById("confirmBody").innerHTML =
        `Bạn có chắc chắn muốn xóa chủ đề:<br>` +
        `<b>${escapeHtml(topic.title)}</b>?<br>` +
        `<span style="color:var(--muted)">Các học phần trong chủ đề này sẽ được chuyển về "Chủ đề mặc định". Hành động này không thể hoàn tác.</span>`;
    openConfirm();
};

function requestEditCourse(course) {
    pendingEditCourse = course || null;
    if (!pendingEditCourse) return;

    const title = document.getElementById("editCourseTitle");
    const language = document.getElementById("editCourseLanguage");
    const cover = document.getElementById("editCourseCoverImage");
    const overlay = document.getElementById("courseEditOverlay");

    if (title) title.value = pendingEditCourse.title || "";
    if (cover) cover.value = pendingEditCourse.coverImagePath || pendingEditCourse.coverImageUrl || "";
    if (language) {
        language.innerHTML = COURSE_LANGUAGE_OPTIONS.map(opt =>
            `<option value="${escapeHtml(opt.code)}">${escapeHtml(opt.label)}</option>`
        ).join("");

        const current = normalizeCourseLanguageCode(pendingEditCourse.languageCode || "");
        language.value = COURSE_LANGUAGE_OPTIONS.some(opt => opt.code === current) ? current : "en";
    }

    if (overlay) overlay.classList.add("show");
    setTimeout(() => title?.focus(), 40);
}

function closeCourseEdit() {
    document.getElementById("courseEditOverlay")?.classList.remove("show");
    pendingEditCourse = null;
}

window.closeCourseEdit = closeCourseEdit;

window.saveCourseEdit = function () {
    if (!pendingEditCourse) {
        closeCourseEdit();
        return;
    }

    const title = document.getElementById("editCourseTitle")?.value.trim() || "";
    const langSelect = document.getElementById("editCourseLanguage");
    const coverImageSource = document.getElementById("editCourseCoverImage")?.value.trim() || "";
    const languageCode = normalizeCourseLanguageCode(langSelect?.value || "");
    const language = langSelect?.selectedOptions?.[0]?.textContent?.trim() || languageCode;

    if (!title) {
        showToast("Nhập tên học phần trước khi lưu.");
        return;
    }

    sendToBackend("updateCourse", {
        id: pendingEditCourse.id,
        title,
        language,
        languageCode,
        coverImageSource
    });

    closeCourseEdit();
    showToast("Đang lưu học phần...");
};

window.pickCourseCoverImage = function () {
    sendToBackend("pickCourseCoverImage", {});
};

window.handleCourseCoverPicked = function (path) {
    const cover = document.getElementById("editCourseCoverImage");
    if (cover) cover.value = path || "";
};

window.courseUpdateDone = function (course) {
    if (course && course.id) {
        const index = allCourses.findIndex(x => x.id === course.id);
        if (index >= 0) allCourses[index] = { ...allCourses[index], ...course };

        if (selectedSet && selectedSet.id === course.id) {
            selectedSet = { ...selectedSet, ...course };
            updateSelectedUI();
            refreshHeroText();
        }

        renderCourses(allCourses);
    }

    if (course && course.coverImageFailed) {
        showToast("Khong lay duoc anh tu link nay. Hay thu link anh truc tiep hoac chon anh tu may.", "warn");
        return;
    }

    showToast(course && course.languageChanged
        ? "Đã đổi ngôn ngữ. App đang tạo lại audio cache."
        : "Đã lưu học phần.");
};

let pendingEditTopic = null;

window.requestCreateTopic = function () {
    pendingEditTopic = null;
    document.getElementById("topicModalTitle").textContent = "Tạo chủ đề mới";
    document.getElementById("editTopicId").value = "";
    document.getElementById("editTopicTitle").value = "";
    document.getElementById("editTopicCoverImage").value = "";
    document.getElementById("topicEditOverlay").classList.add("show");
    setTimeout(() => document.getElementById("editTopicTitle")?.focus(), 40);
};

window.requestEditTopic = function (topic) {
    pendingEditTopic = topic;
    document.getElementById("topicModalTitle").textContent = "Sửa chủ đề";
    document.getElementById("editTopicId").value = topic.id;
    document.getElementById("editTopicTitle").value = topic.title || "";
    document.getElementById("editTopicCoverImage").value = topic.coverImagePath || topic.coverImageUrl || "";
    document.getElementById("topicEditOverlay").classList.add("show");
    setTimeout(() => document.getElementById("editTopicTitle")?.focus(), 40);
};

window.closeTopicEdit = function () {
    document.getElementById("topicEditOverlay").classList.remove("show");
    pendingEditTopic = null;
};

window.saveTopicEdit = function () {
    const title = document.getElementById("editTopicTitle").value.trim();
    const coverImageSource = document.getElementById("editTopicCoverImage").value.trim();
    const id = document.getElementById("editTopicId").value;

    if (!title) {
        showToast("Nhập tên chủ đề trước khi lưu.");
        return;
    }

    if (id) {
        sendToBackend("updateTopic", {
            id,
            title,
            coverImageSource
        });
        showToast("Đang lưu chủ đề...");
    } else {
        sendToBackend("createTopic", {
            title,
            coverImageSource
        });
        showToast("Đang tạo chủ đề...");
    }
    window.closeTopicEdit();
};

window.pickTopicCoverImage = function () {
    sendToBackend("pickTopicCoverImage", {});
};

window.handleTopicCoverPicked = function (path) {
    const cover = document.getElementById("editTopicCoverImage");
    if (cover) cover.value = path || "";
};

window.topicUpdateDone = function (topic) {
    sendToBackend("getTopics");
    showToast("Đã cập nhật chủ đề.");
};

function normalizeCourseLanguageCode(code) {
    const value = String(code || "").trim();
    const lower = value.toLowerCase();
    if (!lower || lower === "zh" || lower === "zh-tw" || lower === "zh-hant" || lower === "zh-hk" || lower === "zh-mo") return "zh-TW";
    if (lower === "zh-cn" || lower === "zh-hans" || lower === "zh-sg") return "zh-CN";
    return value || "en";
}

function openConfirm() {
    document.getElementById("confirmOverlay").classList.add("show");
}

function closeConfirm() {
    document.getElementById("confirmOverlay").classList.remove("show");
    pendingDelete = null;
}

window.closeConfirm = closeConfirm;

window.confirmOk = function () {
    if (!pendingDelete) {
        closeConfirm();
        return;
    }

    if (pendingDeleteType === "topic") {
        sendToBackend("deleteTopic", { id: pendingDelete.id });
        allTopics = allTopics.filter(x => x.id !== pendingDelete.id);
        closeConfirm();
        showToast("Đã xóa chủ đề.");
        sendToBackend("getTopics");
    } else {
        sendToBackend("deleteCourse", { id: pendingDelete.id });

        const deletingId = pendingDelete.id;
        allCourses = allCourses.filter(x => x.id !== deletingId);

        if (selectedSet && selectedSet.id === deletingId) {
            selectedSet = null;
            updateSelectedUI();
        }

        window.updateCourses(allCourses);
        closeConfirm();
        showToast("Đã xóa học phần.");
        refreshHeroText();
    }
};

let toastTimer = null;

function showToast(text, type = "info") {
    if (window.__unifiedToast) {
        window.__unifiedToast(text, type || "info");
        return;
    }

    const t = document.getElementById("toast");
    if (!t) return;

    t.textContent = text || "Thông báo";
    t.classList.add("show");

    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => t.classList.remove("show"), 1800);
}

window.setNotifyState = function (enabled) {
    const btn = document.getElementById("btnNotify");
    if (btn) btn.textContent = enabled ? "Đang nhắc" : "Nhắc từ";
};

window.autoSelectCourse = function (courseInfo) {
    selectedSet = {
        id: courseInfo.id,
        title: courseInfo.title,
        count: courseInfo.count,
        language: courseInfo.language || "",
        languageCode: courseInfo.languageCode || ""
    };
    updateSelectedUI();
    refreshHeroText();
};
