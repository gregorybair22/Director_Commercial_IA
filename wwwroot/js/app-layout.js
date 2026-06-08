window.appLayout = {
    toggleSidebar: function () {
        const shell = document.getElementById('app-shell');
        if (!shell) return false;
        shell.classList.toggle('is-sidebar-collapsed');
        return shell.classList.contains('is-sidebar-collapsed');
    }
};
