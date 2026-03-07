import Toybox.Lang;
import Toybox.WatchUi;

class MainMenuDelegate extends WatchUi.Menu2InputDelegate {
    function initialize() {
        Menu2InputDelegate.initialize();
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var id = item.getId();
        if (id == :today) {
            WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            ApiClient.getTodayTasks(method(:onTodayTasksReceived));
        } else if (id == :templates) {
            WatchUi.pushView(new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
            ApiClient.getTemplates(method(:onTemplatesReceived));
        }
    }

    function onTodayTasksReceived(responseCode as Number, data as Dictionary or String or Null) as Void {
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
