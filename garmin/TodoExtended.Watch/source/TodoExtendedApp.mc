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
        var view = new TemplatesView();
        return [view, new TemplatesDelegate(view)];
    }

    function onSettingsChanged() as Void {
        WatchUi.requestUpdate();
    }
}
