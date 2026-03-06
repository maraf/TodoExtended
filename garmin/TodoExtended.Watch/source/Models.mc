using Toybox.Lang;

module Models {

    class TodoTask {
        var id as String;
        var title as String;
        var isCompleted as Boolean;
        var importance as String;
        var listId as String;
        var listName as String;

        function initialize(data as Dictionary) {
            id = data.get("id") as String;
            title = data.get("title") as String;
            isCompleted = data.get("isCompleted") as Boolean;
            importance = data.get("importance") as String;
            listId = data.get("listId") as String;
            listName = data.get("listName") as String;
        }

        function getDisplayTitle() as String {
            if (title.length() > 20) {
                return title.substring(0, 20) + "...";
            }
            return title;
        }

        function getSubLabel() as String {
            var status = isCompleted ? "Done" : "Todo";
            return listName + " - " + status;
        }
    }

    class Template {
        var id as String;
        var title as String;
        var sortOrder as Number;

        function initialize(data as Dictionary) {
            id = data.get("id") as String;
            title = data.get("title") as String;
            sortOrder = (data.get("sortOrder") as Number);
        }

        function getDisplayTitle() as String {
            if (title.length() > 20) {
                return title.substring(0, 20) + "...";
            }
            return title;
        }
    }

    function parseTasks(data as Array) as Array<TodoTask> {
        var tasks = new Array<TodoTask>[data.size()];
        for (var i = 0; i < data.size(); i++) {
            tasks[i] = new TodoTask(data[i] as Dictionary);
        }
        return tasks;
    }

    function parseTemplates(data as Array) as Array<Template> {
        var templates = new Array<Template>[data.size()];
        for (var i = 0; i < data.size(); i++) {
            templates[i] = new Template(data[i] as Dictionary);
        }
        return templates;
    }
}
