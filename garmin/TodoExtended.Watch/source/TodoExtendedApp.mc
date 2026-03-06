using Toybox.Application;
using Toybox.Lang;
using Toybox.WatchUi;

class TodoExtendedApp extends Application.AppBase {

    function initialize() {
        AppBase.initialize();
    }

    function onStart(state as Dictionary?) as Void {
    }

    function onStop(state as Dictionary?) as Void {
    }

    function getInitialView() as Array<Views or InputDelegates>? {
        if (!Settings.isConfigured()) {
            return [new TodayView(), new TodayDelegate()] as Array<Views or InputDelegates>;
        }
        return [new TodayView(), new TodayDelegate()] as Array<Views or InputDelegates>;
    }

    function onSettingsChanged() as Void {
        WatchUi.requestUpdate();
    }
}
