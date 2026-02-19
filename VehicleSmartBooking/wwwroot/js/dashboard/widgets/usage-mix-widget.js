(() => {
    const colors = ["#0d6efd", "#20c997", "#ffc107", "#6c757d"]; // bootstrap-ish

    const parseJson = (value) => {
        if (!value) {
            return [];
        }
        try {
            return JSON.parse(value);
        } catch {
            return [];
        }
    };

    const initChart = (canvas) => {
        if (typeof Chart === "undefined") {
            return;
        }

        const labels = parseJson(canvas.dataset.chartLabels);
        const values = parseJson(canvas.dataset.chartValues);

        if (!labels.length || !values.length) {
            return;
        }

        const background = labels.map((_, index) => colors[index % colors.length]);

        new Chart(canvas, {
            type: "doughnut",
            data: {
                labels,
                datasets: [{
                    data: values,
                    backgroundColor: background,
                    borderWidth: 0
                }]
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

    const initWidget = (widget) => {
        widget.querySelectorAll(".js-usage-mix-chart").forEach(initChart);
    };

    document.addEventListener("dashboard:widgetLoaded", (event) => {
        if (event.detail?.id !== "usage-mix") {
            return;
        }
        if (event.detail?.element) {
            initWidget(event.detail.element);
        }
    });
})();
