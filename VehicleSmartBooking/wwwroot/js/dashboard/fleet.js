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

    const renderDonut = (canvas, labels, values) => {
        if (!canvas || typeof Chart === "undefined") {
            return;
        }

        destroyChart(canvas);

        new Chart(canvas, {
            type: "doughnut",
            data: {
                labels,
                datasets: [
                    {
                        data: values,
                        backgroundColor: ["#0d6efd", "#ffc107", "#dc3545", "#6c757d"],
                        borderWidth: 0
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: "bottom"
                    }
                }
            }
        });
    };

    const initWidget = async (widget) => {
        const chart = widget.querySelector(".js-fleet-status-chart");
        if (!chart) {
            return;
        }

        const url = chart.dataset.statusUrl;
        const data = await fetchJson(url);
        const items = data?.items ?? [];
        const labels = items.map(x => x.label);
        const values = items.map(x => x.count ?? 0);
        renderDonut(chart, labels, values);
    };

    document.addEventListener("dashboard:widgetLoaded", (event) => {
        if (event.detail?.id !== "fleet-snapshot") {
            return;
        }
        if (event.detail?.element) {
            void initWidget(event.detail.element);
        }
    });
})();
