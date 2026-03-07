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

    for (var i = 0; i < templates.size(); i++) {
        var template = templates[i];
        customMenu.addItem(new TemplateItem(template.id, template.title));
    }

    WatchUi.switchToView(customMenu, new TemplatesDelegate(), WatchUi.SLIDE_UP);
}

class TemplatesMenuTitle extends WatchUi.Drawable {
    function initialize() {
        Drawable.initialize({});
    }

    function draw(dc as Graphics.Dc) as Void {
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_BLACK);
        dc.clear();
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_TRANSPARENT);
        dc.drawText(dc.getWidth() / 2, dc.getHeight() / 2, Graphics.FONT_SMALL, "Templates",
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
        dc.setColor(Graphics.COLOR_BLACK, Graphics.COLOR_TRANSPARENT);

        // Draw label with truncation
        var labelX = spacing * 2;
        var labelY = dc.getHeight() / 2 - 2;
        var dotsWidth = isLoading ? dc.getTextWidthInPixels(" ...", font) : 0;
        var labelAvailableWidth = dc.getWidth() - spacing * 3 - dotsWidth;
        var numberOfSkippedChars = 0;
        var label = _label;
        var labelWidth = dc.getTextWidthInPixels(label, font);
        while (labelWidth > labelAvailableWidth) {
            numberOfSkippedChars++;
            label = _label.substring(0, _label.length() - numberOfSkippedChars) + "..";
            labelWidth = dc.getTextWidthInPixels(label, font);
        }

        if (isLoading) {
            var dotCount = 3 - (loadingValue.toNumber() / 1000) % 3;
            if (dotCount == 1) { label = label + " ."; }
            else if (dotCount == 2) { label = label + " .."; }
            else { label = label + " ..."; }
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
        var custom = item as TemplateItem;
        custom.isLoading = true;
        WatchUi.animate(custom, :loadingValue, WatchUi.ANIM_TYPE_LINEAR, 0, 9000, 9, null);

        var templateId = custom.getId() as String;
        ApiClient.executeTemplate(templateId, new ExecuteTemplateCallback(custom).method(:onResult));
    }
}

class ExecuteTemplateCallback {
    private var _item as TemplateItem;

    function initialize(item as TemplateItem) {
        _item = item;
    }

    function onResult(responseCode as Number, data as Dictionary or String or Null) as Void {
        _item.isLoading = false;
        WatchUi.requestUpdate();
        if (responseCode == 200) {
            WatchUi.showToast("Task created", {});
        } else {
            WatchUi.showToast("Failed (" + responseCode + ")", {});
        }
    }
}
