import commandBase = require("commands/commandBase");
import endpoints = require("endpoints");

class saveGlobalStudioConfigurationCommand extends commandBase {
    
    constructor(private dto: Raven.Client.ServerWide.Operations.Configuration.ServerWideStudioConfiguration) {
        super();
    }
    
    execute(): JQueryPromise<void> {
        const url = endpoints.global.adminConfiguration.adminConfigurationStudio;
        return this.put<void>(url, JSON.stringify(this.dto), null, { dataType: undefined})
            .fail((response: JQueryXHR) => this.reportError(`Failed to save the global studio configuration`, response.responseText, response.statusText)) 
            .done(() => this.reportSuccess("Global studio configuration saved successfully"));
    }
}

export = saveGlobalStudioConfigurationCommand;
