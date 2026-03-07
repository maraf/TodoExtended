import Toybox.Graphics;
import Toybox.Lang;
import Toybox.System;
import Toybox.WatchUi;

function switchToTodayMenu(tasks as Array<Models.TodoTask>) as Void {
    var settings = System.getDeviceSettings();
    var titleHeight = 78;

    var customMenu = new WatchUi.CustomMenu(titleHeight, Graphics.COLOR_WHITE, {
        :focusItemHeight => settings.screenHeight - (2 * titleHeight),
        :title => new TodayMenuTitle(),
        :footer => new TodayMenuFooter()
    });

    for (var i = 0; i < tasks.size(); i++) {
        var task = tasks[i];
        customMenu.addItem(new TodayItem(task.id, task.listId, task.title, task.isCompleted));
    }

    WatchUi.switchToView(customMenu, new TodayDelegate(), WatchUi.SLIDE_UP);
}

class TodayMenuTitle extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.clear();
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);
        dc.drawText(dc.getWidth() / 2, dc.getHeight() / 2, Graphics.FONT_SMALL, "Today",
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
    }
}

class TodayMenuFooter extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.fillRectangle(0, 0, dc.getWidth(), dc.getHeight());
    }
}

class TodayItem extends WatchUi.CustomMenuItem {
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

class TodayDelegate extends WatchUi.Menu2InputDelegate {
    function initialize() {
        Menu2InputDelegate.initialize();
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var custom = item as TodayItem;
        if (custom.isCompleted()) {
            return;
        }

        custom.isLoading = true;
        WatchUi.animate(custom, :loadingValue, WatchUi.ANIM_TYPE_LINEAR, 1440, 0, 4, null);

        var taskId = custom.getId() as String;
        var listId = custom.getListId();
        ApiClient.completeTask(listId, taskId, new CompleteTaskCallback(custom).method(:onResult));
    }
}

class CompleteTaskCallback {
    private var _item as TodayItem;

    function initialize(item as TodayItem) {
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

class Loading extends WatchUi.Drawable {
    var start = 0;

    function initialize(settings) {
        Drawable.initialize(settings);
    }

    function draw(dc as Graphics.Dc) as Void {
        var input = start.toNumber();
        var penWidth = 4;
        dc.setPenWidth(penWidth);

        var currentStart = input % 360;
        var currentEnd = (input + 90) % 360;

        dc.drawArc(locX, locY, width / 2 - penWidth - 6, Graphics.ARC_COUNTER_CLOCKWISE, currentStart, currentEnd);
        dc.setPenWidth(1);
    }
}
