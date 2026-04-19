import Toybox.Attention;
import Toybox.Lang;
import Toybox.System;
import Toybox.WatchUi;

class MainMenuDelegate extends WatchUi.Menu2InputDelegate {
    private var _todayLoadStartTime as Number?;

    function initialize() {
        Menu2InputDelegate.initialize();
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var id = item.getId();
        _todayLoadStartTime = System.getTimer();
        if (id == :today) {
            WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            ApiClient.getTodayTasks(method(:onTodayTasksReceived));
        } else if (id == :templates) {
            WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            ApiClient.getTemplates(method(:onTemplatesReceived));
        } else if (id == :taskLists) {
            WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            ApiClient.getTaskLists(method(:onTaskListsReceived));
        } else if (id == :tags) {
            WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            ApiClient.getFavoriteTags(method(:onFavoriteTagsReceived));
        }
    }

    private function _vibrateIfLoadingWasSlow() as Void {
        if (_todayLoadStartTime != null) {
            var startTime = _todayLoadStartTime as Number;
            var elapsed = System.getTimer() - startTime;
            _todayLoadStartTime = null;
            if (elapsed > 5000 && (Attention has :vibrate)) {
                Attention.vibrate([new Attention.VibeProfile(100, 300)]);
            }
        }
    }

    function onTodayTasksReceived(responseCode as Number, data as Dictionary or String or Null) as Void {
        _vibrateIfLoadingWasSlow();
        Attention.backlight(true);
        if (responseCode == 200 && data != null) {
            var tasks = Models.parseTasks(data as Array);
            switchToTodayMenu(tasks);
        } else {
            WatchUi.popView(WatchUi.SLIDE_DOWN);
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.showToast(message, {});
        }
    }

    function onTemplatesReceived(responseCode as Number, data as Dictionary or String or Null) as Void {
        _vibrateIfLoadingWasSlow();
        Attention.backlight(true);
        if (responseCode == 200 && data != null) {
            var templates = Models.parseTemplates(data as Array);
            switchToTemplatesMenu(templates);
        } else {
            WatchUi.popView(WatchUi.SLIDE_DOWN);
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.showToast(message, {});
        }
    }

    function onTaskListsReceived(responseCode as Number, data as Dictionary or String or Null) as Void {
        _vibrateIfLoadingWasSlow();
        Attention.backlight(true);
        if (responseCode == 200 && data != null) {
            var lists = Models.parseTaskLists(data as Array);
            switchToTaskListsMenu(lists);
        } else {
            WatchUi.popView(WatchUi.SLIDE_DOWN);
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.showToast(message, {});
        }
    }

    function onFavoriteTagsReceived(responseCode as Number, data as Dictionary or String or Null) as Void {
        _vibrateIfLoadingWasSlow();
        Attention.backlight(true);
        if (responseCode == 200 && data != null) {
            var tags = Models.parseFavoriteTags(data as Array);
            switchToTagsMenu(tags);
        } else {
            WatchUi.popView(WatchUi.SLIDE_DOWN);
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.showToast(message, {});
        }
    }
}
