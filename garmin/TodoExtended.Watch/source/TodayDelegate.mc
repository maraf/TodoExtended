import Toybox.Lang;
import Toybox.WatchUi;

class TodayDelegate extends WatchUi.BehaviorDelegate {

    private var _view as TodayView;

    function initialize(view as TodayView) {
        BehaviorDelegate.initialize();
        _view = view;
    }

    function onSelect() as Boolean {
        var view = _view;
        if (view == null || view.isLoading() || view.hasError()) {
            return true;
        }

        var tasks = view.getTasks();
        if (tasks == null || tasks.size() == 0) {
            return true;
        }

        _showTaskMenu(tasks);
        return true;
    }

    private function _showTaskMenu(tasks as Array<Models.TodoTask>) as Void {
        var menu = new WatchUi.Menu2({
            :title => "Today's Tasks"
        });

        for (var i = 0; i < tasks.size(); i++) {
            var task = tasks[i];
            var icon = task.isCompleted ? "* " : "  ";
            menu.addItem(new WatchUi.MenuItem(
                icon + task.getDisplayTitle(),
                task.getSubLabel(),
                task.id,
                {}
            ));
        }

        WatchUi.pushView(menu, new TaskMenuDelegate(tasks), WatchUi.SLIDE_LEFT);
    }

    function onNextPage() as Boolean {
        // Swipe up / next page → navigate to Templates
        var templatesView = new TemplatesView();
        WatchUi.switchToView(templatesView, new TemplatesDelegate(templatesView), WatchUi.SLIDE_UP);
        return true;
    }
}

class TaskMenuDelegate extends WatchUi.Menu2InputDelegate {

    private var _tasks as Array<Models.TodoTask>;

    function initialize(tasks as Array<Models.TodoTask>) {
        Menu2InputDelegate.initialize();
        _tasks = tasks;
    }

    function onSelect(item as WatchUi.MenuItem) as Void {
        var taskId = item.getId() as String;
        var task = _findTask(taskId);
        if (task != null) {
            var detailView = new TaskDetailView(task);
            WatchUi.pushView(detailView, new TaskDetailDelegate(task, detailView), WatchUi.SLIDE_LEFT);
        }
    }

    private function _findTask(id as String) as Models.TodoTask? {
        for (var i = 0; i < _tasks.size(); i++) {
            if (_tasks[i].id.equals(id)) {
                return _tasks[i];
            }
        }
        return null;
    }
}
