import Toybox.Graphics;
import Toybox.Lang;
import Toybox.System;
import Toybox.WatchUi;

function switchToTaskListsMenu(lists as Array<Models.TodoTaskList>) as Void {
    var settings = System.getDeviceSettings();
    var titleHeight = 78;

    var customMenu = new WatchUi.CustomMenu(titleHeight, Graphics.COLOR_WHITE, {
        :focusItemHeight => settings.screenHeight - (2 * titleHeight),
        :title => new TaskListsMenuTitle(),
        :footer => new TaskListsMenuFooter()
    });

    for (var i = 0; i < lists.size(); i++) {
        var list = lists[i];
        customMenu.addItem(new TaskListItem(list.id, list.displayName));
    }

    WatchUi.switchToView(customMenu, new TaskListsDelegate(), WatchUi.SLIDE_UP);
}

class TaskListsMenuTitle extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.clear();
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);
        dc.drawText(dc.getWidth() / 2, dc.getHeight() / 2, Graphics.FONT_SMALL, "Task Lists",
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
    }
}

class TaskListsMenuFooter extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.fillRectangle(0, 0, dc.getWidth(), dc.getHeight());
    }
}

class TaskListItem extends WatchUi.CustomMenuItem {
    private var _label as String;

    function initialize(id as String, text as String) {
        CustomMenuItem.initialize(id, {});
        _label = text;
    }

    function getDisplayName() as String {
        return _label;
    }

    function draw(dc as Graphics.Dc) as Void {
        var font = Graphics.FONT_TINY;
        if (isFocused()) {
            font = Graphics.FONT_MEDIUM;
        }

        var spacing = 8;
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_TRANSPARENT);

        // Draw label with truncation
        var labelX = spacing * 2;
        var labelY = dc.getHeight() / 2 - 2;
        var labelAvailableWidth = dc.getWidth() - spacing * 3;
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
}

class TaskListsDelegate extends WatchUi.Menu2InputDelegate {
    function initialize() {
        Menu2InputDelegate.initialize();
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var listId = item.getId() as String;
        var label = (item as TaskListItem).getDisplayName();
        WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null),
                        new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
        ApiClient.getTaskListTasks(listId, new TaskListTasksCallback(listId, label).method(:onResult));
    }
}

class TaskListTasksCallback {
    private var _listId as String;
    private var _listName as String;

    function initialize(listId as String, listName as String) {
        _listId = listId;
        _listName = listName;
    }

    function onResult(responseCode as Number, data as Dictionary or String or Null) as Void {
        if (responseCode == 200 && data != null) {
            var tasks = Models.parseTaskItems(data as Array);
            switchToTaskListTasksMenu(_listId, _listName, tasks);
        } else {
            WatchUi.popView(WatchUi.SLIDE_DOWN);
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.showToast(message, {});
        }
    }
}
