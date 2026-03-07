import Toybox.Application;
import Toybox.Application.Properties;
import Toybox.Lang;

module Settings {

    function getApiBaseUrl() as String {
        var url = Application.Properties.getValue("apiBaseUrl") as String;
        if (url == null || url.length() == 0) {
            return "";
        }
        // Strip trailing slash for consistent URL building
        if (url.substring(url.length() - 1, url.length()).equals("/")) {
            return url.substring(0, url.length() - 1);
        }
        return url;
    }

    function getApiKey() as String {
        var key = Application.Properties.getValue("apiKey") as String;
        if (key == null) {
            return "";
        }
        return key;
    }

    function isConfigured() as Boolean {
        return getApiBaseUrl().length() > 0 && getApiKey().length() > 0;
    }
}
