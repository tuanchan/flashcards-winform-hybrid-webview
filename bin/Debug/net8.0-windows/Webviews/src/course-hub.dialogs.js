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
    document.getElementById("confirmTitle").textContent = "Xóa học phần";
    document.getElementById("confirmBody").innerHTML =
        `Bạn có chắc chắn muốn xóa học phần:<br>` +
        `<b>${escapeHtml(course.title)}</b>?<br>` +
        `<span style="color:var(--muted)">Hành động này không thể hoàn tác.</span>`;
    openConfirm();
}

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
