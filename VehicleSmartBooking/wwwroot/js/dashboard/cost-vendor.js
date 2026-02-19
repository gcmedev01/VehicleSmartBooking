(() => {
    const buildUrl = (baseUrl) => {
        if (!baseUrl) {
            return "";
        }
        const query = window.location.search;
        if (!query) {
            return baseUrl;
        }
        return baseUrl.includes("?") ? `${baseUrl}&${query.substring(1)}` : `${baseUrl}${query}`;
    };

    const fetchJson = async (url) => {
        if (!url) {
            return null;
        }
        const response = await fetch(buildUrl(url), {
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "application/json"
            }
        });
        if (!response.ok) {
            return null;
        }
        return response.json();
    };

    const destroyChart = (canvas) => {
        const existing = Chart.getChart(canvas);
        if (existing) {
            existing.destroy();
        }
    };

    const renderLine = (canvas, labels, data, label, color) => {
        if (!canvas || typeof Chart === "undefined") {
            return;
        }

        destroyChart(canvas);

        new Chart(canvas, {
            type: "line",
            data: {
                labels,
                datasets: [
                    {
                        label,
                        data,
                        borderColor: color,
                        backgroundColor: color,
                        tension: 0.2
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    }
                }
            }
        });
    };

    const renderCost = (canvas, data, label, color) => {
        const months = data?.months ?? [];
        const labels = months.map(x => x.label);
        const values = months.map(x => x.totalCost ?? 0);
        renderLine(canvas, labels, values, label, color);
    };

    const initWidget = async (widget) => {
        const vendorCanvas = widget.querySelector(".js-vendor-cost-chart");
        const personalCanvas = widget.querySelector(".js-personal-cost-chart");

        if (vendorCanvas) {
            const url = vendorCanvas.dataset.costUrl;
            const data = await fetchJson(url);
            renderCost(vendorCanvas, data, "Vendor Cost", "#0d6efd");
        }

        if (personalCanvas) {
            const url = personalCanvas.dataset.costUrl;
            const data = await fetchJson(url);
            renderCost(personalCanvas, data, "Personal Claim", "#ffc107");
        }
    };

    document.addEventListener("dashboard:widgetLoaded", (event) => {
        if (event.detail?.id !== "cost-vendor") {
            return;
        }
        if (event.detail?.element) {
            void initWidget(event.detail.element);
        }
    });
})();
