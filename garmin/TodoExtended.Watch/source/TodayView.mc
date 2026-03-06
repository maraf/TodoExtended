using Toybox.Graphics;
using Toybox.Lang;
using Toybox.System;
using Toybox.WatchUi;

class TodayView extends WatchUi.View {

    private var _loading as Boolean = true;
    private var _error as String? = null;
    private var _tasks as Array<Models.TodoTask>? = null;

    function initialize() {
        View.initialize();
    }

    function onLayout(dc as Graphics.Dc) as Void {
    }

    function onShow() as Void {
        loadTasks();
    }

    function loadTasks() as Void {
        if (!Settings.isConfigured()) {
            _loading = false;
            _error = "Configure API URL\nand key in\nGarmin Connect";
            WatchUi.requestUpdate();
            return;
        }

        _loading = true;
        _error = null;
        WatchUi.requestUpdate();
        ApiClient.getTodayTasks(method(:onTasksReceived));
    }

    function onTasksReceived(responseCode as Number, data as Dictionary or Array or Null) as Void {
        _loading = false;
        if (responseCode == 200 && data != null) {
            _tasks = Models.parseTasks(data as Array);
            _error = null;
        } else {
            _tasks = null;
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

        if (_tasks == null || _tasks.size() == 0) {
            _drawCentered(dc, "No tasks today!");
            return;
        }

        // Show task count header then switch to menu on select
        _drawCentered(dc, _tasks.size() + " tasks today\nPress to view");
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

    function getTasks() as Array<Models.TodoTask>? {
        return _tasks;
    }

    function hasError() as Boolean {
        return _error != null;
    }

    function isLoading() as Boolean {
        return _loading;
    }
}
