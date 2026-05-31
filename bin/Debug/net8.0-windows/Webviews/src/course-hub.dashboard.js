// Dashboard statistics and chart rendering

window.showStatistics = function () {
    const screen = document.getElementById("dashboardScreen");
    document.body.classList.add("dashboard-mode");
    if (screen) {
        screen.classList.add("show");
        screen.setAttribute("aria-hidden", "false");
    }
    sendToBackend("getDashboardStats");
};

window.closeDashboard = function () {
    const screen = document.getElementById("dashboardScreen");
    document.body.classList.remove("dashboard-mode");
    if (screen) {
        screen.classList.remove("show");
        screen.setAttribute("aria-hidden", "true");
    }
};

window.applyDashboardStats = function (stats) {
    if (!stats) return;

    // Update numbers
    document.getElementById("valTotalCards").textContent = stats.totalCards;
    document.getElementById("valTotalLanguages").textContent = stats.totalLanguages;
    document.getElementById("valTotalDue").textContent = stats.totalDue;
    document.getElementById("valTotalHard").textContent = stats.totalHard;
    const memorizedEl = document.getElementById("valTotalMemorized");
    if (memorizedEl) memorizedEl.textContent = stats.memorizedCount || 0;
    const unlearnedEl = document.getElementById("valTotalUnlearned");
    if (unlearnedEl) unlearnedEl.textContent = stats.unlearnedCount || 0;

    // Render widgets
    renderSrsDistribution(stats.srsDistribution);
    renderDueTimeline(stats.dueTimeline);
    renderLanguageRings(stats.languageDistribution);
    renderStatusGauges(stats.rates);
    renderHardCoursesChart(stats.topHardCourses);
};

function renderSrsDistribution(srsDistribution) {
    const srsBarList = document.getElementById("srsBarList");
    if (!srsBarList) return;

    // Group levels: Mastered (7-8), Studying (4-6), Learning (1-3), New/Lapsed (0)
    const mastered = (srsDistribution[7] || 0) + (srsDistribution[8] || 0);
    const studying = (srsDistribution[4] || 0) + (srsDistribution[5] || 0) + (srsDistribution[6] || 0);
    const learning = (srsDistribution[1] || 0) + (srsDistribution[2] || 0) + (srsDistribution[3] || 0);
    const brandNew = srsDistribution[0] || 0;

    const total = mastered + studying + learning + brandNew || 1;

    const groups = [
        { label: "Cấp 7-8 (Thành thạo)", count: mastered, color: "#10b981" },
        { label: "Cấp 4-6 (Đang ôn tốt)", count: studying, color: "#3e5cff" },
        { label: "Cấp 1-3 (Mới học)", count: learning, color: "#8b5cf6" },
        { label: "Cấp 0 (Chưa thuộc / Mới)", count: brandNew, color: "#ef4444" }
    ];

    srsBarList.innerHTML = groups.map(g => {
        const pct = ((g.count / total) * 100).toFixed(1);
        return `
            <div class="srs-bar-item">
                <div class="srs-bar-meta">
                    <span class="srs-bar-label">${g.label}</span>
                    <span class="srs-bar-count"><b>${g.count}</b> (${pct}%)</span>
                </div>
                <div class="srs-bar-bg">
                    <div class="srs-bar-fill" style="width: ${pct}%; background-color: ${g.color};"></div>
                </div>
            </div>
        `;
    }).join("");
}

function renderDueTimeline(dueTimeline) {
    const container = document.getElementById("dueTimelineChart");
    if (!container) return;

    const width = 500;
    const height = 160;
    const padding = { top: 20, right: 20, bottom: 25, left: 35 };

    const maxVal = Math.max(...dueTimeline, 5); // Ensure scale has at least 5 max height
    const labels = ["Hôm nay", "Mai", "+2 ngày", "+3 ngày", "+4 ngày", "+5 ngày", "+6 ngày"];

    // Compute coordinate points
    const points = dueTimeline.map((val, idx) => {
        const x = padding.left + (idx / 6) * (width - padding.left - padding.right);
        const y = height - padding.bottom - (val / maxVal) * (height - padding.top - padding.bottom);
        return { x, y, val };
    });

    // Generate SVG path for a smooth bezier curve
    let pathD = "";
    if (points.length > 0) {
        pathD = `M ${points[0].x} ${points[0].y}`;
        for (let i = 0; i < points.length - 1; i++) {
            const p0 = points[i];
            const p1 = points[i + 1];
            const cpX1 = p0.x + (p1.x - p0.x) / 2;
            const cpY1 = p0.y;
            const cpX2 = p0.x + (p1.x - p0.x) / 2;
            const cpY2 = p1.y;
            pathD += ` C ${cpX1} ${cpY1}, ${cpX2} ${cpY2}, ${p1.x} ${p1.y}`;
        }
    }

    const baseline = height - padding.bottom;
    const areaD = `${pathD} L ${points[points.length - 1].x} ${baseline} L ${points[0].x} ${baseline} Z`;

    // Y Grid lines
    let yGrid = "";
    const yTicks = 4;
    for (let i = 0; i <= yTicks; i++) {
        const y = padding.top + (i / yTicks) * (height - padding.top - padding.bottom);
        const val = Math.round(maxVal - (i / yTicks) * maxVal);
        yGrid += `
            <line x1="${padding.left}" y1="${y}" x2="${width - padding.right}" y2="${y}" stroke="rgba(255,255,255,0.06)" stroke-dasharray="3,3" />
            <text x="${padding.left - 8}" y="${y + 4}" fill="#8e92a2" font-size="9" text-anchor="end" font-weight="700">${val}</text>
        `;
    }

    // X Labels & data nodes
    let xLabels = "";
    points.forEach((p, idx) => {
        xLabels += `
            <text x="${p.x}" y="${height - 8}" fill="#8e92a2" font-size="9" text-anchor="middle" font-weight="700">${labels[idx]}</text>
            <circle cx="${p.x}" cy="${p.y}" r="3.5" fill="#3e5cff" stroke="#ffffff" stroke-width="1.5" />
            <text x="${p.x}" y="${p.y - 8}" fill="#ffffff" font-size="9" font-weight="900" text-anchor="middle">${p.val}</text>
        `;
    });

    container.innerHTML = `
        <svg viewBox="0 0 ${width} ${height}" class="timeline-svg" style="width: 100%; height: 100%;">
            <defs>
                <linearGradient id="areaGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#3e5cff" stop-opacity="0.3" />
                    <stop offset="100%" stop-color="#3e5cff" stop-opacity="0.0" />
                </linearGradient>
            </defs>
            <g>${yGrid}</g>
            <path d="${areaD}" fill="url(#areaGrad)" />
            <path d="${pathD}" fill="none" stroke="#3e5cff" stroke-width="2.5" stroke-linecap="round" />
            <g>${xLabels}</g>
        </svg>
    `;
}

function renderLanguageRings(languages) {
    const chart = document.getElementById("langRingsChart");
    const legend = document.getElementById("langRingsLegend");
    if (!chart || !legend) return;

    const topLangs = (languages || []).slice(0, 3);
    const totalCards = topLangs.reduce((sum, item) => sum + item.count, 0) || 1;

    const colors = ["#3e5cff", "#8b5cf6", "#10b981"];
    const radiusList = [40, 28, 16];
    const strokeWidth = 8;
    const center = 50;

    let svgRings = "";
    let legendHtml = "";

    topLangs.forEach((lang, idx) => {
        const radius = radiusList[idx] || 16;
        const color = colors[idx] || "#8e92a2";
        const circumference = 2 * Math.PI * radius;
        const pct = lang.count / totalCards;
        const offset = circumference * (1 - pct);

        svgRings += `
            <circle cx="${center}" cy="${center}" r="${radius}" fill="none" stroke="rgba(255,255,255,0.03)" stroke-width="${strokeWidth}" />
            <circle cx="${center}" cy="${center}" r="${radius}" fill="none" stroke="${color}" stroke-width="${strokeWidth}"
                    stroke-dasharray="${circumference}" stroke-dashoffset="${offset}" stroke-linecap="round"
                    transform="rotate(-90 ${center} ${center})" />
        `;

        legendHtml += `
            <div class="legend-item">
                <span class="legend-color" style="background-color: ${color};"></span>
                <span class="legend-label">${escapeHtml(lang.lang)}</span>
                <span class="legend-value">${lang.count} thẻ (${(pct * 100).toFixed(0)}%)</span>
            </div>
        `;
    });

    if (topLangs.length === 0) {
        svgRings = `<text x="50" y="55" fill="#8e92a2" font-size="8" font-weight="700" text-anchor="middle">Không có dữ liệu</text>`;
        legendHtml = `<div class="legend-empty">Chưa có học phần nào</div>`;
    }

    chart.innerHTML = `
        <svg viewBox="0 0 100 100" class="rings-svg" style="width: 108px; height: 108px; flex: none;">
            ${svgRings}
        </svg>
    `;
    legend.innerHTML = legendHtml;
}

function renderStatusGauges(rates) {
    const container = document.getElementById("statusGauges");
    if (!container) return;

    const gauges = [
        { label: "Đã thuộc", value: rates.memorized, color: "#10b981" },
        { label: "Đang ôn", value: rates.studying, color: "#3e5cff" },
        { label: "Thẻ khó", value: rates.hard, color: "#ef4444" }
    ];

    const center = 40;
    const radius = 30;
    const circumference = 2 * Math.PI * radius;

    container.innerHTML = gauges.map(g => {
        const offset = circumference * (1 - g.value / 100);
        return `
            <div class="gauge-item">
                <div class="gauge-chart">
                    <svg viewBox="0 0 80 80" class="gauge-svg">
                        <circle cx="${center}" cy="${center}" r="${radius}" fill="none" stroke="rgba(255,255,255,0.03)" stroke-width="6" />
                        <circle cx="${center}" cy="${center}" r="${radius}" fill="none" stroke="${g.color}" stroke-width="6"
                                stroke-dasharray="${circumference}" stroke-dashoffset="${offset}" stroke-linecap="round"
                                transform="rotate(-90 ${center} ${center})" />
                        <text x="${center}" y="${center + 4}" fill="#ffffff" font-size="11" font-weight="900" text-anchor="middle">
                            ${g.value.toFixed(0)}%
                        </text>
                    </svg>
                </div>
                <div class="gauge-label">${g.label}</div>
            </div>
        `;
    }).join("");
}

function renderHardCoursesChart(topHardCourses) {
    const container = document.getElementById("hardCoursesChart");
    if (!container) return;

    if (topHardCourses.length === 0) {
        container.innerHTML = `<div class="chart-empty">Không có thẻ khó nào</div>`;
        return;
    }

    const maxHard = Math.max(...topHardCourses.map(c => c.hardCount), 1);

    container.innerHTML = topHardCourses.map(c => {
        const heightPct = (c.hardCount / maxHard) * 100;
        return `
            <div class="bar-column">
                <div class="bar-top-value">${c.hardCount}</div>
                <div class="bar-track">
                    <div class="bar-fill" style="height: ${heightPct}%; background: linear-gradient(180deg, #ef4444, #b91c1c);"></div>
                </div>
                <div class="bar-label" title="${escapeHtml(c.title)}">${escapeHtml(c.title)}</div>
            </div>
        `;
    }).join("");
}

function escapeHtml(str) {
    if (!str) return "";
    return str
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}
