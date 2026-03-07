import Toybox.Communications;
import Toybox.Lang;
import Toybox.System;

module ApiClient {

    // Error codes
    const ERROR_NO_CONNECTION = -104;
    const ERROR_RESPONSE_TOO_LARGE = -402;

    typedef ApiCallback as Method(responseCode as Number, data as Dictionary or String or Null) as Void;

    function getTodayTasks(callback as ApiCallback) as Void {
        var url = Settings.getApiBaseUrl() + "/api/today";
        _makeGetRequest(url, callback);
    }

    function getTemplates(callback as ApiCallback) as Void {
        var url = Settings.getApiBaseUrl() + "/api/templates";
        _makeGetRequest(url, callback);
    }

    function executeTemplate(templateId as String, callback as ApiCallback) as Void {
        var url = Settings.getApiBaseUrl() + "/api/templates/" + templateId + "/execute";
        _makePostRequest(url, callback);
    }

    function completeTask(listId as String, taskId as String, callback as ApiCallback) as Void {
        var url = Settings.getApiBaseUrl() + "/api/tasks/" + listId + "/" + taskId + "/complete";
        _makePostRequest(url, callback);
    }

    function _makeGetRequest(url as String, callback as ApiCallback) as Void {
        var options = {
            :method => Communications.HTTP_REQUEST_METHOD_GET,
            :headers => {
                "Content-Type" => Communications.REQUEST_CONTENT_TYPE_JSON,
                "X-Api-Key" => Settings.getApiKey()
            },
            :responseType => Communications.HTTP_RESPONSE_CONTENT_TYPE_JSON
        };
        Communications.makeWebRequest(url, {}, options, callback);
    }

    function _makePostRequest(url as String, callback as ApiCallback) as Void {
        var options = {
            :method => Communications.HTTP_REQUEST_METHOD_POST,
            :headers => {
                "Content-Type" => Communications.REQUEST_CONTENT_TYPE_JSON,
                "X-Api-Key" => Settings.getApiKey()
            },
            :responseType => Communications.HTTP_RESPONSE_CONTENT_TYPE_JSON
        };
        Communications.makeWebRequest(url, {}, options, callback);
    }

    function getErrorMessage(responseCode as Number) as String {
        switch (responseCode) {
            case ERROR_NO_CONNECTION:
                return "No phone connection";
            case -300:
                return "Network error";
            case -400:
                return "Invalid response";
            case -401:
                return "Parse error";
            case ERROR_RESPONSE_TOO_LARGE:
                return "Response too large";
            case 401:
                return "Invalid API key";
            case 403:
                return "Access denied";
            case 404:
                return "Not found";
            case 500:
                return "Server error";
            default:
                if (responseCode < 0) {
                    return "Connection error";
                }
                return "Error: " + responseCode;
        }
    }
}
