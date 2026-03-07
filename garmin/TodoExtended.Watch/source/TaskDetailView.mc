import Toybox.Graphics;
import Toybox.Lang;
import Toybox.WatchUi;

class TaskDetailView extends WatchUi.View {

    private var _task as Models.TodoTask;
    private var _completing as Boolean = false;
    private var _result as String? = null;

    function initialize(task as Models.TodoTask) {
        View.initialize();
        _task = task;
    }

    function onUpdate(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.clear();

        var width = dc.getWidth();
        var height = dc.getHeight();
        var centerX = width / 2;

        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);

        if (_completing) {
            dc.drawText(centerX, height / 2, Graphics.FONT_SMALL,
                "Completing...", Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
            return;
        }

        if (_result != null) {
            dc.drawText(centerX, height / 2, Graphics.FONT_SMALL,
                _result as String, Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
            return;
        }

        // Draw task detail
        var statusIcon = _task.isCompleted ? "[Done]" : "[Todo]";
        var importanceColor = _getImportanceColor();

        // Title
        dc.drawText(centerX, height / 2 - 40, Graphics.FONT_SMALL,
            _task.getDisplayTitle(), Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);

        // List name
        dc.setColor(Graphics.COLOR_LT_GRAY, Graphics.COLOR_TRANSPARENT);
        dc.drawText(centerX, height / 2, Graphics.FONT_XTINY,
            _task.listName, Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);

        // Status + importance
        dc.setColor(importanceColor, Graphics.COLOR_TRANSPARENT);
        dc.drawText(centerX, height / 2 + 25, Graphics.FONT_XTINY,
            statusIcon + " " + _task.importance,
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);

        // Action hint
        if (!_task.isCompleted) {
            dc.setColor(Graphics.COLOR_GREEN, Graphics.COLOR_TRANSPARENT);
            dc.drawText(centerX, height / 2 + 55, Graphics.FONT_XTINY,
                "Press to complete",
                Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
        }
    }

    private function _getImportanceColor() as Number {
        if (_task.importance.equals("high")) {
            return Graphics.COLOR_RED;
        } else if (_task.importance.equals("low")) {
            return Graphics.COLOR_DK_GRAY;
        }
        return Graphics.COLOR_WHITE;
    }

    function getTask() as Models.TodoTask {
        return _task;
    }

    function isCompleting() as Boolean {
        return _completing;
    }

    function setCompleting(completing as Boolean) as Void {
        _completing = completing;
        WatchUi.requestUpdate();
    }

    function setResult(result as String) as Void {
        _result = result;
        _completing = false;
        WatchUi.requestUpdate();
    }
}

class TaskDetailDelegate extends WatchUi.BehaviorDelegate {

    private var _task as Models.TodoTask;
    private var _view as TaskDetailView;

    function initialize(task as Models.TodoTask, view as TaskDetailView) {
        BehaviorDelegate.initialize();
        _task = task;
        _view = view;
    }

    function onSelect() as Boolean {
        if (_task.isCompleted) {
            return true;
        }

        var view = _view;
        if (view != null && !view.isCompleting()) {
            view.setCompleting(true);
            ApiClient.completeTask(_task.listId, _task.id, method(:onCompleteResult));
        }
        return true;
    }

    function onCompleteResult(responseCode as Number, data as Dictionary or String or Null) as Void {
        var view = _view;
        if (view == null) {
            return;
        }

        if (responseCode == 200) {
            _task.isCompleted = true;
            view.setResult("Task completed!");
        } else {
            view.setResult(ApiClient.getErrorMessage(responseCode));
        }
    }
}
