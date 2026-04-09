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
        customMenu.addItem(new TodayItem(task.id, task.listId, getTagTaskTitle(tagName, task.title), task.isCompleted));
    }

    WatchUi.switchToView(customMenu, new TodayDelegate(), WatchUi.SLIDE_UP);
}

function getTagTaskTitle(tagName as String, title as String) as String {
    if (tagName.length() == 0 || title.length() == 0) {
        return title;
    }

    var titleLower = title.toLower();
    var tagPrefix = "#" + tagName;
    var tagPrefixLower = tagPrefix.toLower();
    var trimStart = getTagPrefixTrimStart(title, titleLower, tagPrefix, tagPrefixLower);

    if (trimStart == -1) {
        var tagNameLower = tagName.toLower();
        trimStart = getTagPrefixTrimStart(title, titleLower, tagName, tagNameLower);
    }

    if (trimStart == -1) {
        return title;
    }

    while (trimStart < title.length() && title.substring(trimStart, trimStart + 1).equals(" ")) {
        trimStart++;
    }

    var trimmedTitle = title.substring(trimStart, title.length());
    return trimmedTitle.length() > 0 ? trimmedTitle : title;
}

function getTagPrefixTrimStart(title as String, titleLower as String, prefix as String, prefixLower as String) as Number {
    if (titleLower.length() < prefixLower.length() ||
        !titleLower.substring(0, prefixLower.length()).equals(prefixLower)) {
        return -1;
    }

    if (title.length() == prefix.length()) {
        return prefix.length();
    }

    var nextChar = title.substring(prefix.length(), prefix.length() + 1);
    if (nextChar.equals(" ") || nextChar.equals("-") || nextChar.equals(":")) {
        return prefix.length();
    }

    return -1;
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
        while (labelWidth > maxWidth && numberOfSkippedChars < _title.length() - 2) {
            numberOfSkippedChars++;
            label = _title.substring(0, _title.length() - numberOfSkippedChars) + "..";
            labelWidth = dc.getTextWidthInPixels(label, font);
        }

        if (labelWidth > maxWidth) {
            label = "..";
            labelWidth = dc.getTextWidthInPixels(label, font);
            if (labelWidth > maxWidth) {
                label = "";
            }
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
