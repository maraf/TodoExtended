using Toybox.Lang;
using Toybox.WatchUi;

class TemplatesDelegate extends WatchUi.BehaviorDelegate {

    function initialize() {
        BehaviorDelegate.initialize();
    }

    function onSelect() as Boolean {
        var view = WatchUi.getCurrentView() as TemplatesView;
        if (view == null || view.isLoading() || view.hasError()) {
            return true;
        }

        var templates = view.getTemplates();
        if (templates == null || templates.size() == 0) {
            return true;
        }

        _showTemplateMenu(templates);
        return true;
    }

    private function _showTemplateMenu(templates as Array<Models.Template>) as Void {
        var menu = new WatchUi.Menu2({
            :title => "Templates"
        });

        for (var i = 0; i < templates.size(); i++) {
            var template = templates[i];
            menu.addItem(new WatchUi.MenuItem(
                template.getDisplayTitle(),
                "Tap to create",
                template.id,
                {}
            ));
        }

        WatchUi.pushView(menu, new TemplateMenuDelegate(templates), WatchUi.SLIDE_LEFT);
    }

    function onPreviousPage() as Boolean {
        // Swipe down / previous page → back to Today
        var todayView = new TodayView();
        WatchUi.switchToView(todayView, new TodayDelegate(), WatchUi.SLIDE_DOWN);
        return true;
    }
}

class TemplateMenuDelegate extends WatchUi.Menu2InputDelegate {

    private var _templates as Array<Models.Template>;

    function initialize(templates as Array<Models.Template>) {
        Menu2InputDelegate.initialize();
        _templates = templates;
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var templateId = item.getId() as String;
        var template = _findTemplate(templateId);
        if (template != null) {
            _executeTemplate(template, item);
        }
    }

    private function _findTemplate(id as String) as Models.Template? {
        for (var i = 0; i < _templates.size(); i++) {
            if (_templates[i].id.equals(id)) {
                return _templates[i];
            }
        }
        return null;
    }

    private function _executeTemplate(template as Models.Template, item as WatchUi.MenuItem) as Void {
        item.setSubLabel("Creating...");
        WatchUi.requestUpdate();
        ApiClient.executeTemplate(template.id, method(:onExecuteResult));
    }

    function onExecuteResult(responseCode as Number, data as Dictionary or Array or Null) as Void {
        if (responseCode == 200) {
            // Pop the template menu and go back, then refresh today view
            WatchUi.popView(WatchUi.SLIDE_RIGHT);
            var todayView = new TodayView();
            WatchUi.switchToView(todayView, new TodayDelegate(), WatchUi.SLIDE_RIGHT);
        } else {
            // Show error via a simple confirmation dialog
            var message = ApiClient.getErrorMessage(responseCode);
            var dialog = new WatchUi.Confirmation(message);
            WatchUi.pushView(dialog, new WatchUi.ConfirmationDelegate(), WatchUi.SLIDE_IMMEDIATE);
        }
    }
}
