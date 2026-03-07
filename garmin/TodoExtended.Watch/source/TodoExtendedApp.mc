import Toybox.Application;
import Toybox.Lang;
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

        var menu = new WatchUi.Menu2({ :title => "TodoEx" });
        menu.addItem(new WatchUi.MenuItem("Today", null, :today, {}));
        menu.addItem(new WatchUi.MenuItem("Templates", null, :templates, {}));
        return [menu, new MainMenuDelegate()];
    }

    function onSettingsChanged() as Void {
        WatchUi.requestUpdate();
    }
}
