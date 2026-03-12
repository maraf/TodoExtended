import Toybox.Graphics;
import Toybox.Lang;
import Toybox.System;
import Toybox.WatchUi;

function switchToTaskListTasksMenu(listId as String, listName as String, tasks as Array<Models.TaskItem>) as Void {
    var settings = System.getDeviceSettings();
    var titleHeight = 78;

    var customMenu = new WatchUi.CustomMenu(titleHeight, Graphics.COLOR_WHITE, {
        :focusItemHeight => settings.screenHeight - (2 * titleHeight),
        :title => new TaskListTasksMenuTitle(listName),
        :footer => new TaskListTasksMenuFooter()
    });

    for (var i = 0; i < tasks.size(); i++) {
        var task = tasks[i];
        customMenu.addItem(new TaskListTaskItem(task.id, listId, task.title, task.isCompleted));
    }

    WatchUi.switchToView(customMenu, new TaskListTasksDelegate(), WatchUi.SLIDE_UP);
}

class TaskListTasksMenuTitle extends WatchUi.Drawable {
    private var _title as String;

    function initialize(title as String) {
        Drawable.initialize({});
        _title = title;
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.clear();
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);

        // Truncate title if needed
        var font = Graphics.FONT_SMALL;
        var maxWidth = dc.getWidth() - 16;
        var numberOfSkippedChars = 0;
        var label = _title;
        var labelWidth = dc.getTextWidthInPixels(label, font);
        while (labelWidth > maxWidth) {
            numberOfSkippedChars++;
            label = _title.substring(0, _title.length() - numberOfSkippedChars) + "..";
            labelWidth = dc.getTextWidthInPixels(label, font);
        }

        dc.drawText(dc.getWidth() / 2, dc.getHeight() / 2, font, label,
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
    }
}

class TaskListTasksMenuFooter extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.fillRectangle(0, 0, dc.getWidth(), dc.getHeight());
    }
}

class TaskListTaskItem extends WatchUi.CustomMenuItem {
    private var _label as String;
    private var _listId as String;
    private var _isCompleted as Boolean;
    private var _checkBitmap as WatchUi.BitmapResource?;

    var loading as Loading?;
    var isLoading = false;
    var loadingValue = 0;

    function initialize(id as String, listId as String, text as String, isCompleted as Boolean) {
        CustomMenuItem.initialize(id, {});
        _label = text;
        _listId = listId;
        _isCompleted = isCompleted;
        loadCheckBitmap();
    }

    function draw(dc as Graphics.Dc) as Void {
        var font = Graphics.FONT_TINY;
        if (isFocused()) {
            font = Graphics.FONT_MEDIUM;
        }

        var spacing = 8;

        if (isFocused()) {
            dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_TRANSPARENT);

            if (isLoading) {
                if (loading == null) {
                    loading = new Loading({
                        :locX => spacing + 4 + _checkBitmap.getWidth() / 2,
                        :locY => dc.getHeight() / 2,
                        :width => _checkBitmap.getWidth()
                    });
                }

                loading.start = loadingValue;
                loading.draw(dc);
            } else {
                dc.drawBitmap(spacing + 4, (dc.getHeight() - _checkBitmap.getHeight()) / 2, _checkBitmap);
            }
        }

        dc.setColor(_isCompleted ? Graphics.COLOR_DK_GRAY : Graphics.COLOR_BLACK, Graphics.COLOR_TRANSPARENT);

        // Draw label with truncation
        var labelX = _checkBitmap.getWidth() + spacing * 2;
        var labelY = dc.getHeight() / 2 - 2;
        var labelAvailableWidth = dc.getWidth() - labelX - spacing;
        var numberOfSkippedChars = 0;
        var label = _label;
        var labelWidth = dc.getTextWidthInPixels(label, font);
        while (labelWidth > labelAvailableWidth) {
            numberOfSkippedChars++;
            label = _label.substring(0, _label.length() - numberOfSkippedChars) + "..";
            labelWidth = dc.getTextWidthInPixels(label, font);
        }

        dc.drawText(labelX, labelY, font, label, Graphics.TEXT_JUSTIFY_LEFT | Graphics.TEXT_JUSTIFY_VCENTER);

        // Separator lines
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.drawLine(0, 0, dc.getWidth(), 0);
        dc.drawLine(0, dc.getHeight(), dc.getWidth(), dc.getHeight());
    }

    function getListId() as String {
        return _listId;
    }

    function isCompleted() as Boolean {
        return _isCompleted;
    }

    function setCompleted(isCompleted as Boolean) as Void {
        _isCompleted = isCompleted;
        loadCheckBitmap();
    }

    private function loadCheckBitmap() as Void {
        _checkBitmap = WatchUi.loadResource(_isCompleted ? $.Rez.Drawables.CheckIcon : $.Rez.Drawables.UncheckIcon) as WatchUi.BitmapResource;
    }
}

class TaskListTasksDelegate extends WatchUi.Menu2InputDelegate {
    function initialize() {
        Menu2InputDelegate.initialize();
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var custom = item as TaskListTaskItem;
        if (custom.isCompleted()) {
            return;
        }

        custom.isLoading = true;
        WatchUi.animate(custom, :loadingValue, WatchUi.ANIM_TYPE_LINEAR, 1440, 0, 4, null);

        var taskId = custom.getId() as String;
        var listId = custom.getListId();
        ApiClient.completeTask(listId, taskId, new CompleteTaskListItemCallback(custom).method(:onResult));
    }
}

class CompleteTaskListItemCallback {
    private var _item as TaskListTaskItem;

    function initialize(item as TaskListTaskItem) {
        _item = item;
    }

    function onResult(responseCode as Number, data as Dictionary or String or Null) as Void {
        if (responseCode == 200) {
            _item.setCompleted(true);
            _item.isLoading = false;
            WatchUi.requestUpdate();
        } else {
            _item.isLoading = false;
            WatchUi.requestUpdate();
            WatchUi.showToast("Failed (" + responseCode + ")", {});
        }
    }
}
