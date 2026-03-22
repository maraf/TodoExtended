import Toybox.Graphics;
import Toybox.Lang;
import Toybox.System;
import Toybox.WatchUi;

function switchToTagTasksMenu(tagName as String, tasks as Array<Models.TodoTask>) as Void {
    var settings = System.getDeviceSettings();
    var titleHeight = 78;

    var customMenu = new WatchUi.CustomMenu(titleHeight, Graphics.COLOR_WHITE, {
        :focusItemHeight => settings.screenHeight - (2 * titleHeight),
        :title => new TagTasksMenuTitle(tagName),
        :footer => new TagTasksMenuFooter()
    });

    for (var i = 0; i < tasks.size(); i++) {
        var task = tasks[i];
        customMenu.addItem(new TodayItem(task.id, task.listId, task.title, task.isCompleted));
    }

    WatchUi.switchToView(customMenu, new TodayDelegate(), WatchUi.SLIDE_UP);
}

class TagTasksMenuTitle extends WatchUi.Drawable {
    private var _title as String;

    function initialize(tagName as String) {
        Drawable.initialize({});
        _title = "#" + tagName;
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

class TagTasksMenuFooter extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.fillRectangle(0, 0, dc.getWidth(), dc.getHeight());
    }
}
