(() => {
    const widgetSelector = ".dashboard-widget";
    const registrySelector = "[data-dashboard-registry]";
    const filterFormSelector = ".dashboard-filter-bar form";
    const resetSelector = "[data-dashboard-reset], .js-dashboard-reset, button[type=reset]";

    const buildUrl = (baseUrl) => {
        const query = window.location.search;
        if (!query) {
            return baseUrl;
        }
        return baseUrl.includes("?") ? `${baseUrl}&${query.substring(1)}` : `${baseUrl}${query}`;
    };

    const readRegistry = () => {
        const registryEl = document.querySelector(registrySelector);
        if (!registryEl) {
            return [];
        }

        const raw = registryEl.dataset.dashboardRegistry;
        if (!raw) {
            return [];
        }

        try {
            const parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    };

    const resolveWidgets = () => {
        const registry = readRegistry();
        if (registry.length === 0) {
            return Array.from(document.querySelectorAll(widgetSelector));
        }

        return registry
            .map((item) => {
                if (!item?.id) {
                    return null;
                }
                const widget = document.querySelector(`${widgetSelector}[data-widget-id="${item.id}"]`);
                if (widget && item.url) {
                    widget.dataset.widgetUrl = item.url;
                }
                return widget;
            })
            .filter(Boolean);
    };

    const loadWidget = async (widget) => {
        const url = widget.dataset.widgetUrl;
        if (!url) {
            return;
        }

        const body = widget.querySelector(".dashboard-widget-body");
        if (!body) {
            return;
        }

        try {
            const response = await fetch(buildUrl(url), {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (!response.ok) {
                throw new Error(`Widget load failed: ${response.status}`);
            }

            body.innerHTML = await response.text();
            widget.classList.remove("is-loading");

            document.dispatchEvent(new CustomEvent("dashboard:widgetLoaded", {
                detail: {
                    id: widget.dataset.widgetId,
                    element: widget
                }
            }));
        } catch {
            body.innerHTML = "<div class=\"text-danger\">Unable to load widget.</div>";
            widget.classList.remove("is-loading");
        }
    };

    const reloadWithFilters = (form) => {
        const formData = new FormData(form);
        const params = new URLSearchParams();
        for (const [key, value] of formData.entries()) {
            if (value !== null && value !== "") {
                params.append(key, value.toString());
            }
        }

        const target = window.location.pathname;
        const queryString = params.toString();
        window.location.href = queryString ? `${target}?${queryString}` : target;
    };

    const wireFilters = () => {
        const form = document.querySelector(filterFormSelector);
        if (!form) {
            return;
        }

        form.addEventListener("submit", (event) => {
            event.preventDefault();
            reloadWithFilters(form);
        });

        form.addEventListener("reset", () => {
            window.location.href = window.location.pathname;
        });

        form.querySelectorAll(resetSelector).forEach((button) => {
            button.addEventListener("click", () => {
                window.location.href = window.location.pathname;
            });
        });
    };

    const init = () => {
        resolveWidgets().forEach((widget) => {
            if (widget) {
                loadWidget(widget);
            }
        });
        wireFilters();
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
