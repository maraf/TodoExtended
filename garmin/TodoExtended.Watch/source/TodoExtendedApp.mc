import Toybox.Application;
import Toybox.Lang;
import Toybox.System;
import Toybox.WatchUi;

class TodoExtendedApp extends Application.AppBase {

    function initialize() {
        AppBase.initialize();
    }

    function onStart(state as Dictionary?) as Void {
    }

    function onStop(state as Dictionary?) as Void {
    }

    function getInitialView() as [Views] or [Views, InputDelegates] {
        if (!Settings.isConfigured()) {
            return [new MessageView("Configure API URL\nand key in\nGarmin Connect"), new WatchUi.BehaviorDelegate()];
        }

        ApiClient.getTodayTasks(method(:onTodayTasksReceived));
        return [new WatchUi.ProgressBar("Loading...", null), new WatchUi.BehaviorDelegate()];
    }

    function onTodayTasksReceived(responseCode as Number, data as Dictionary or String or Null) as Void {
        if (responseCode == 200 && data != null) {
            var tasks = Models.parseTasks(data as Array);
            switchToTodayMenu(tasks);
        } else {
            var message = ApiClient.getErrorMessage(responseCode);
            WatchUi.switchToView(new MessageView(message), new WatchUi.BehaviorDelegate(), WatchUi.SLIDE_UP);
        }
    }

    function onSettingsChanged() as Void {
        WatchUi.requestUpdate();
    }
}
