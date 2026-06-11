let isDarkMode = false;
let selectedSet = null;
let allCourses = [];
let allTopics = [];
let currentView = "topics";
let selectedTopic = null;
let pendingDelete = null;
let sidebarVisible = false;
let sortMode = "default";
let languageFilter = "all";

const __courseCollator = new Intl.Collator("vi", {
    numeric: true,
    sensitivity: "base"
});
