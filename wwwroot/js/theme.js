window.rentLogTheme = {
    get: function () {
        var storedTheme = localStorage.getItem("rentlog-theme");
        if (storedTheme === "light" || storedTheme === "dark") {
            document.documentElement.setAttribute("data-theme", storedTheme);
            return storedTheme;
        }

        document.documentElement.removeAttribute("data-theme");
        return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    },
    set: function (theme) {
        if (theme !== "light" && theme !== "dark") {
            throw new Error("Unsupported Rent Log theme.");
        }

        localStorage.setItem("rentlog-theme", theme);
        document.documentElement.setAttribute("data-theme", theme);
    }
};

window.rentLogTheme.get();
