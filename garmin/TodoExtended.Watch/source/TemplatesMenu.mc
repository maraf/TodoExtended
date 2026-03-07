import Toybox.Graphics;
import Toybox.Lang;
import Toybox.System;
import Toybox.WatchUi;

function switchToTemplatesMenu(templates as Array<Models.Template>) as Void {
    var settings = System.getDeviceSettings();
    var titleHeight = 78;

    var customMenu = new WatchUi.CustomMenu(titleHeight, Graphics.COLOR_WHITE, {
        :focusItemHeight => settings.screenHeight - (2 * titleHeight),
        :title => new TemplatesMenuTitle(),
        :footer => new TemplatesMenuFooter()
    });

    customMenu.addItem(new NavItem("nav-today", "\u00AB Today"));

    for (var i = 0; i < templates.size(); i++) {
        var template = templates[i];
        customMenu.addItem(new TemplateItem(template.id, template.title));
    }

    WatchUi.switchToView(customMenu, new TemplatesDelegate(), WatchUi.SLIDE_DOWN);
}

class TemplatesMenuTitle extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        var spacing = 4;

        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.clear();

        // App name
        var appLabelY = dc.getHeight() / 2 - spacing;
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);
        dc.drawText(dc.getWidth() / 2, appLabelY, Graphics.FONT_SMALL, "TodoExtended",
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);

        // Tab indicators
        var tabY = dc.getHeight() - spacing - dc.getTextDimensions("Templates", Graphics.FONT_XTINY)[1] / 2;
        var centerX = dc.getWidth() / 2;

        // Inactive: Today
        dc.setColor(Graphics.COLOR_DK_GRAY, Graphics.COLOR_TRANSPARENT);
        dc.drawText(centerX - 30, tabY, Graphics.FONT_XTINY, "Today",
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);

        // Active: Templates
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);
        dc.drawText(centerX + 40, tabY, Graphics.FONT_XTINY, "\u25B8 Templates",
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
    }
}

class TemplatesMenuFooter extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.fillRectangle(0, 0, dc.getWidth(), dc.getHeight());
    }
}

class TemplateItem extends WatchUi.CustomMenuItem {
    private var _label as String;

    var loading as Loading?;
    var isLoading = false;
    var loadingValue = 0;

    function initialize(id as String, text as String) {
        CustomMenuItem.initialize(id, {});
        _label = text;
    }

    function draw(dc as Graphics.Dc) as Void {
        var font = Graphics.FONT_TINY;
        if (isFocused()) {
            font = Graphics.FONT_MEDIUM;
        }

        var spacing = 8;

        if (isFocused() && isLoading) {
            dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_TRANSPARENT);
            if (loading == null) {
                loading = new Loading({
                    :locX => spacing + 16,
                    :locY => dc.getHeight() / 2,
                    :width => 28
                });
            }

            loading.start = loadingValue;
            loading.draw(dc);
        }

        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_TRANSPARENT);

        // Draw label with truncation
        var labelX = spacing;
        var labelY = dc.getHeight() / 2 - 2;
        var labelAvailableWidth = dc.getWidth() - spacing * 2;
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

class TemplatesDelegate extends WatchUi.Menu2InputDelegate {
    function initialize() {
        Menu2InputDelegate.initialize();
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        if (item instanceof NavItem) {
            // Navigate to Today tab
            WatchUi.switchToView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_DOWN);
            ApiClient.getTodayTasks(method(:onTodayReceived));
            return;
        }

        var custom = item as TemplateItem;
        custom.isLoading = true;
        WatchUi.animate(custom, :loadingValue, WatchUi.ANIM_TYPE_LINEAR, 1440, 0, 4, null);

        var templateId = custom.getId() as String;
        ApiClient.executeTemplate(templateId, method(:onExecuteResult));
    }

    function onExecuteResult(responseCode as Number, data as Dictionary or String or Null) as Void {
        if (responseCode == 200) {
            // After executing template, load and show today tasks
            WatchUi.switchToView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            ApiClient.getTodayTasks(method(:onTodayReceived));
        } else {
            WatchUi.showToast("Failed (" + responseCode + ")", {});
        }
    }

    function onTodayReceived(responseCode as Number, data as Dictionary or String or Null) as Void {
        if (responseCode == 200 && data != null) {
            var tasks = Models.parseTasks(data as Array);
            switchToTodayMenu(tasks);
        } else {
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.showToast(message, {});
        }
    }
}
