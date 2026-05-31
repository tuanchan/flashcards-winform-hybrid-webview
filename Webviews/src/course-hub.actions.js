function setupEventListeners() {
    const si = document.getElementById("searchInput");
    if (si) {
        si.addEventListener("input", e => {
            sendToBackend("searchCourses", { query: e.target.value });
        });
    }

    const ss = document.getElementById("sortSelect");
    if (ss) {
        ss.addEventListener("change", e => {
            sortMode =
                e && e.target ? e.target.value || "default" : "default";
            renderCourses(allCourses);
        });
    }

    const lang = document.getElementById("languageFilterSelect");
    if (lang) {
        lang.addEventListener("change", e => {
            languageFilter =
                e && e.target ? e.target.value || "all" : "all";
            const selectedOption = e && e.target
                ? e.target.options[e.target.selectedIndex]
                : null;
            if (selectedOption) {
                e.target.title = selectedOption.textContent || "";
                e.target.setAttribute("aria-label", selectedOption.textContent || "Language filter");
            }
            renderCourses(allCourses);
        });
    }

    const dueCheckbox = document.getElementById("dueFilterCheckbox");
    if (dueCheckbox) {
        dueCheckbox.addEventListener("change", () => {
            renderCourses(allCourses);
        });
    }

    document.addEventListener("keydown", e => {
        if (e.key === "Escape") {
            closeConfirm();
            if (typeof closeCourseEdit === "function") closeCourseEdit();
        }

        if (e.key === "F11") {
            e.preventDefault();
            sendToBackend("toggleFullScreen");
        }
    });

    refreshHeroText();
}

window.startQuiz = function () {
    if (typeof closeWritingPractice === "function") closeWritingPractice();
    sendToBackend("startQuiz");
};

window.createCourse = function () {
    if (typeof closeWritingPractice === "function") closeWritingPractice();
    sendToBackend("createCourse");
};

window.showNotifications = function () {
    sendToBackend("showNotifications");
};

window.toggleTheme = function () {
    setDark(!isDarkMode);
};

function setDark(dark) {
    isDarkMode = !!dark;
    document.body.classList.toggle("dark-mode", isDarkMode);

    const bt = document.getElementById("btnTheme");
    if (bt) bt.textContent = isDarkMode ? "Tối" : "Sáng";

    sendToBackend("toggleTheme", { dark: isDarkMode });
    refreshHeroText();
}

window.showCourseList = function () {
    if (typeof closeWritingPractice === "function") closeWritingPractice();
    sidebarVisible = true;
    document.getElementById("sidebar").classList.remove("hidden");
    refreshHeroText();
};

window.toggleSidebar = function () {
    sidebarVisible = !sidebarVisible;
    const s = document.getElementById("sidebar");
    sidebarVisible ? s.classList.remove("hidden") : s.classList.add("hidden");
};

window.showFeatureWithCheck = function (feature) {
    if (typeof closeWritingPractice === "function") closeWritingPractice();
    if (!selectedSet) {
        showToast("Bạn chưa chọn học phần.");
        return;
    }

    sendToBackend("showFeature", { feature });
};
