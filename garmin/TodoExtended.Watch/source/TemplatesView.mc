using Toybox.Graphics;
using Toybox.Lang;
using Toybox.WatchUi;

class TemplatesView extends WatchUi.View {

    private var _loading as Boolean = true;
    private var _error as String? = null;
    private var _templates as Array<Models.Template>? = null;

    function initialize() {
        View.initialize();
    }

    function onShow() as Void {
        loadTemplates();
    }

    function loadTemplates() as Void {
        if (!Settings.isConfigured()) {
            _loading = false;
            _error = "Configure API URL\nand key in\nGarmin Connect";
            WatchUi.requestUpdate();
            return;
        }

        _loading = true;
        _error = null;
        WatchUi.requestUpdate();
        ApiClient.getTemplates(method(:onTemplatesReceived));
    }

    function onTemplatesReceived(responseCode as Number, data as Dictionary or Array or Null) as Void {
        _loading = false;
        if (responseCode == 200 && data != null) {
            _templates = Models.parseTemplates(data as Array);
            _error = null;
        } else {
            _templates = null;
            _error = ApiClient.getErrorMessage(responseCode);
        }
        WatchUi.requestUpdate();
    }

    function onUpdate(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.clear();

        if (_loading) {
            _drawCentered(dc, "Loading...");
            return;
        }

        if (_error != null) {
            _drawCentered(dc, _error as String);
            return;
        }

        if (_templates == null || _templates.size() == 0) {
            _drawCentered(dc, "No templates");
            return;
        }

        _drawCentered(dc, _templates.size() + " templates\nPress to view");
    }

    private function _drawCentered(dc as Graphics.Dc, text as String) as Void {
        var width = dc.getWidth();
        var height = dc.getHeight();
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);
        dc.drawText(
            width / 2,
            height / 2,
            Graphics.FONT_SMALL,
            text,
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER
        );
    }

    function getTemplates() as Array<Models.Template>? {
        return _templates;
    }

    function hasError() as Boolean {
        return _error != null;
    }

    function isLoading() as Boolean {
        return _loading;
    }
}
