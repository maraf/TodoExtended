import Toybox.Graphics;
import Toybox.Lang;
import Toybox.System;
import Toybox.WatchUi;

function switchToTagsMenu(tags as Array<String>) as Void {
    var settings = System.getDeviceSettings();
    var titleHeight = 78;

    var customMenu = new WatchUi.CustomMenu(titleHeight, Graphics.COLOR_WHITE, {
        :focusItemHeight => settings.screenHeight - (2 * titleHeight),
        :title => new TagsMenuTitle(),
        :footer => new TagsMenuFooter()
    });

    for (var i = 0; i < tags.size(); i++) {
        customMenu.addItem(new TagItem(tags[i]));
    }

    WatchUi.switchToView(customMenu, new TagsDelegate(), WatchUi.SLIDE_UP);
}

class TagsMenuTitle extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.clear();
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);
        dc.drawText(dc.getWidth() / 2, dc.getHeight() / 2, Graphics.FONT_SMALL, "Tags",
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
    }
}

class TagsMenuFooter extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.fillRectangle(0, 0, dc.getWidth(), dc.getHeight());
    }
}

class TagItem extends WatchUi.CustomMenuItem {
    private var _label as String;

    function initialize(tag as String) {
        CustomMenuItem.initialize(tag, {});
        _label = "#" + tag;
    }

    function getTagName() as String {
        return getId() as String;
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
        while (labelWidth > labelAvailableWidth && numberOfSkippedChars < _label.length() - 2) {
            numberOfSkippedChars++;
            label = _label.substring(0, _label.length() - numberOfSkippedChars) + "..";
            labelWidth = dc.getTextWidthInPixels(label, font);
        }

        if (labelWidth > labelAvailableWidth) {
            label = "..";
            if (dc.getTextWidthInPixels(label, font) > labelAvailableWidth) {
                label = "";
            }
        }

        dc.drawText(labelX, labelY, font, label, Graphics.TEXT_JUSTIFY_LEFT | Graphics.TEXT_JUSTIFY_VCENTER);

        // Separator lines
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.drawLine(0, 0, dc.getWidth(), 0);
        dc.drawLine(0, dc.getHeight(), dc.getWidth(), dc.getHeight());
    }
}

class TagsDelegate extends WatchUi.Menu2InputDelegate {
    function initialize() {
        Menu2InputDelegate.initialize();
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var tagItem = item as TagItem;
        var tagName = tagItem.getTagName();
        WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null),
                        new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
        ApiClient.getTagTasks(tagName, new TagTasksCallback(tagName).method(:onResult));
    }
}

class TagTasksCallback {
    private var _tagName as String;

    function initialize(tagName as String) {
        _tagName = tagName;
    }

    function onResult(responseCode as Number, data as Dictionary or String or Null) as Void {
        if (responseCode == 200 && data != null) {
            var tasks = Models.parseTasks(data as Array);
            switchToTagTasksMenu(_tagName, tasks);
        } else {
            WatchUi.popView(WatchUi.SLIDE_DOWN);
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.showToast(message, {});
        }
    }
}
