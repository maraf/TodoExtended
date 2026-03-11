import Toybox.Attention;
import Toybox.Lang;
import Toybox.Timer;
import Toybox.WatchUi;

class MainMenuDelegate extends WatchUi.Menu2InputDelegate {
    private var _loadingTimer as Timer.Timer?;

    function initialize() {
        Menu2InputDelegate.initialize();
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var id = item.getId();
        if (id == :today) {
            WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            _startLoadingTimer();
            ApiClient.getTodayTasks(method(:onTodayTasksReceived));
        } else if (id == :templates) {
            WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            ApiClient.getTemplates(method(:onTemplatesReceived));
        }
    }

    private function _startLoadingTimer() as Void {
        _loadingTimer = new Timer.Timer();
        _loadingTimer.start(method(:_onLoadingTimerFired), 1500, false);
    }

    function _onLoadingTimerFired() as Void {
        if (Attention has :vibrate) {
            Attention.vibrate([new Attention.VibeProfile(100, 300)]);
        }
        _loadingTimer = null;
    }

    private function _stopLoadingTimer() as Void {
        if (_loadingTimer != null) {
            _loadingTimer.stop();
            _loadingTimer = null;
        }
    }

    function onTodayTasksReceived(responseCode as Number, data as Dictionary or String or Null) as Void {
        _stopLoadingTimer();
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
        if (responseCode == 200 && data != null) {
            var templates = Models.parseTemplates(data as Array);
            switchToTemplatesMenu(templates);
        } else {
            WatchUi.popView(WatchUi.SLIDE_DOWN);
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.showToast(message, {});
        }
    }
}
