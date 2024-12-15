# FsOpenAI

A rich web application (webassembly) for interacting with OpenAI models, either deployed in Azure or OpenAI.

#### Release Notes
- 2024/12/7
    - fixed signalr/websocket authorization vulnerability (please upgrade to new code base)
    - optional capability available to validate client oauth token expiry on each SignlarR message receive
        + previously this was done only upon connection creation
    - background task added to remove any dangling uploaded or intermediate files in temp folder
    - upload file size limit enfored on server side
    - to build an unauthenticated version of the app now requires compilation with UNAUTHENTICATED symbol: 
        + dotnet build --property:DefineConstants=UNAUTHENTICATED
    - Code Evaluator improvements:
        + the experimental code evaluation capability now requires a special build, its not linked in by default
        + 'chat' with code evaluator now available for testing purposes
        + *Evaluator Sandboxing*
            + generated code is validated by scanning the 'typed' AST to only allow types from a given set of namespaces
            + code evaluation is performed in a separate process so as to not affect the host process
            + evaluation is time bound with limited concurrent evaluations
            + generated code size limit
        + note: code evaluator requires dockerized deployment
    - Configuration Changes 
        + **Important Note:** *appsettings.json* files are excluded from the repo as they may contain proprietary data. Instead **appsettings.json.template** files have been introduced that contain example configuration settings. They may be copied to create local appsettings.json files to make the settings effective.
        + platform-specific path separator ('/' & '\\') handling added
        + the *service settings json* file path specified in *appsettings.json* is now expected to be relative to *HOME* directory, e.g. %USERPROFILE% for Windows and %HOME% for MacOs.
    - Other
        + exception handling improvements to safeguard against leaking internal details
        + code refactor: some large modules refactored into smaller, more coherent modules (among others refactorings)
        + lowercase names for some files (namely *appsettings.json*) to support cross platform development/deployment (windows, macos & linux)

### External Resources
#### [Write up](https://www.linkedin.com/posts/activity-7096903519516983297-56Yg?utm_source=share&utm_medium=member_desktop)

#### [Online sample](https://fsopenaiserver1.azurewebsites.net) | App written with [Bolero / F#](https://github.com/fsbolero/Bolero) framework

#### [Application Arch. description](https://www.linkedin.com/posts/activity-7141858744061022208-P-D7?utm_source=share&utm_medium=member_desktop)

## Overview
FsOpenAI is meant to be deployed to as an Azure Web App. The secrets required to access Azure-deployed models (and other resources) can be stored in an Azure Key Vault for high security and easier management.

In fact, the goal of FsOpenAI is to keep configuration and deployment simple for faster deployments and ease of maintenance.

## FsOpenAI Interaction Modes

Currently, FsOpenAI supports four types of interaction (i.e chat) modes. These are selectable from the available 'Sources' on the UI.

1. **Basic chat**: Interactive chat session with pre-trained 
GPT models, optionally augmented with Bing search results.

2. **Question and answer over custom documents**: Retrieval Augmented Generation (RAG) for Q&A over custom document collections indexed in Azure AI Search (for now).

3. **Document query**: Upload a document and query its contents. Two modes supported: A) document-only model B) document-plus-index mode where document text is combined with index search results (and optionally a custom prompt template) for more complex Q&A.

4. **Chat with Code**: Experimental feature to generate and evaluate F# code. Not meant for production deployments.

The document collections have to be pre-loaded into Azure Search Service indexes. The document collection indexes should support a specific format to enable vector-based searches. The required format is described elsewhere in this document.

## Infrastructure Requirements
To support all interaction types, the following are typically required:

- Azure subscription with access to:
    + [Azure web app service](https://azure.microsoft.com/en-us/free/apps/search/?ef_id=_k_eaa0d546fb08153a5061b19e3c496316_k_&OCID=AIDcmm5edswduu_SEM__k_eaa0d546fb08153a5061b19e3c496316_k_&msclkid=eaa0d546fb08153a5061b19e3c496316)
    + [Azure key vault](https://azure.microsoft.com/en-us/products/key-vault/?ef_id=_k_cfe29a3676c213166ad6feeeb1d8d68b_k_&OCID=AIDcmm5edswduu_SEM__k_cfe29a3676c213166ad6feeeb1d8d68b_k_&msclkid=cfe29a3676c213166ad6feeeb1d8d68b)
    + [Azure OpenAI service](https://azure.microsoft.com/en-us/products/ai-services/openai-service) or [OpenAI API access](https://api.openai.com)
    + [Azure AI search](https://azure.microsoft.com/en-us/products/ai-services/cognitive-search)
    + [CosmosDB](https://azure.microsoft.com/en-us/products/cosmos-db) (optional) for chat logging and session persistence

- FsOpenAI (or a derived version) deployed to Azure Web App. The app should be given a [managed identity](https://learn.microsoft.com/en-us/azure/app-service/overview-managed-identity?tabs=portal%2Chttp) so that it can securely access the key vault. Also see app configuration later in this document.
- Azure key vault with the appropriate secrets loaded and [access from web app configured.](https://learn.microsoft.com/en-us/azure/key-vault/general/tutorial-net-create-vault-azure-web-app)
- One or more chat models deployed to Azure OpenAI service, e.g. *gpt-4o*, *gpt-4o-mini*, etc.
- One or more search indexes containing document collections in the required format. The embeddings vector contained in each record should be created with the same embedding model which will be used to query the document collection.

- Bing search API Azure service (optional)

## Application Configuration

The FsOpenAI application has several configurable parameters, grouped into different categories. Each category has its own file or folder structure associated with it. The categories are first listed below and then explained later in this section.

- Server configuration with secrets and API keys
- Application configuration that enables/disables features and customizes look and feel
- Prompt templates by domain (deprecated)
- Samples
- Authentication configuration for integration with Azure AD

### 1: Server Configuration [or *service settings json*]
The secrets and backend service connection information (e.g. Azure / OpenAI model endpoints, CosmosDB connection string, etc.) are stored in a json file, referred to as *service settings json*. The structure of this file is provided in [ExampleSettings.json](/ExampleSettings.json).

A running application will look for this file in three place (in the order listed):
- Path given by the key *"Parms:Settings"* in *appsettings.json* (server). Path is expected to be relative the *HOME* directory of user. This generally use for local testing and development.
- Environment variable *FSOPENAI_SETTINGS_FILE*
- Azure KeyVault (see below) 

For KeyVault or envrionment variable placements, the entire contents of this file are serialized to a base64 string. See [SerializeSettings.fsx](/src/FsOpenAI.Tasks/scripts/SerializeSettings.fsx).
FsOpenAI will look for the following hardcoded environment variables to pull the settings json from Azure Key Vault.

|Env. Var. Name.|Description|
|----------------|------------|
|FSOPENAI_AZURE_KEYVAULT|Key Vault name| 
|FSOPENAI_AZURE_KEYVAULT_KEY| Key name|

The Azure documentation for configuring web apps is [here.](https://learn.microsoft.com/en-us/azure/app-service/configure-common?tabs=portal)

Multiple instances of FsOpenAI may be deployed, each with its own configuration (e.g. for segregating access by organizational groups, etc.). If Azure AD is the organizational directory, then [each instance can be easily restricted to specfic AD groups](https://learn.microsoft.com/en-us/azure/active-directory/develop/howto-restrict-your-app-to-a-set-of-users).

#### Load Balance Configuration
The [service settings json](/ExampleSettings.json) allows for multiple endpoints (and API keys) to be configured under *AZURE_OPENAI_ENDPOINTS* and *AZURE_SEARCH_ENDPOINTS*. If multiple endpoints are configured, FsOpenAI will randomly select one endpoint before making the API call. This is a form of load balancing to obtain higher overall throughput. FsOpenAI expects that all endpoints are mirror images of each other (respective of the service type).

Note each chat or search API call is independent and stateless (from a server perspective) so 'stickiness' is not required.
### 2. App Configuration

The *AppConfig.json* file is deployed to (server) wwwroot/app/ folder. It contains settings for customizing the appearance of the app and enabling/disabling certain features, e.g. allow plain chat interactions or not. The AppConfig.json should be created using a script like [config_default.fsx](/src/FsOpenAI.Tasks/deployments/default/config_default.fsx). This will ensure type safety. The AppConfig record structure fields are well documented with code comments. Hover mouse cursor over the AppConfig record fields to view the comments and documentation for each setting.

#### The **'app'** Folders: 
Deployment specific application configuration is largely kept in two folders both named *app* under **(client)/wwwroot** and **(server)/wwwroot**. This should make for easier customzation for different deployments as only the *app* folders need be replaced (in most cases) before deployment.

Logos and 'persona' images can be loaded into (client) *wwwroot/app/imgs* folder.

#### Session Persistence and Logging
CosmosDB connection string can be specified in [settings json](/ExampleSettings.json). If a connection is specified then chat sessions will be persisted in CosmosDB. Also chat submission will be logged there. CosmosDB database, container name, etc., can be set in AppConfig.json. See also [Sessions.fs](src/FsOpenAI.GenAI/Sessions.fs) and [Monitoring.fs](src/FsOpenAI.GenAI/Monitoring.fs) for additional details.

### 3. Prompt Templates by Domain (deprecated)

The *Template* folder under (server) *wwwroot/app* folder can contain prompt templates in the [Semantic Kernel plugin format](https://learn.microsoft.com/en-us/semantic-kernel/ai-orchestration/plugins/?tabs=Csharp). 

The following example shows a possible folder structure:
```
wwwroot/app
--Templates
----Legal (domain)
------QnA (plugin or skill)
--------Function 1
----------config.json
----------skprompt.txt
--------function 2
----------config.json
----------skprompt.txt
--------function 3 ...
...
------Summarize (next plugin)
...
---Finance (mext domain)
...
```
There can be several plugins under each of the (business) domains. The prompt templates that fall under a particular business domain can be tailored for that domain. For example the Finance templates may be very specific the needs of the Finance department.

For each domain under *Templates*, the app will add a menu choice for a new *document query* interaction, e.g. if Finance and Legal folders are found under *wwwroot/app/Templates* then the plus (+) (or new chat) menu will show additional choices:
- New 'Finance' Document Query
- New 'Legal' Document Query

When a chat interaction is created using such a menu pick, it will be associated with the plugins and templates under that domain. When interacting with the chat, the user may select an appropriate template for the query in mind. The user may also overwrite the text of the prompt template if required.

Currently, the plugins and templates are associated with the *document query* interaction mode but may be associated with other chat modes in future.

### 4. Samples
For each domain under *wwwroot/app/Templates/(domain)* a *Samples.json"* file may be added. If such a file exits, the application will show each of the samples in that file when the application first launches **and there are no existing saved chats**. For example, the Finance and Legal samples will be shown if the folder structure is as follows:

```
wwwroot/app
--Templates
----Finance
-----Samples.json
-----<Finance plugings>
----Legal
------Samples.json
------<Legal plugins>
```
The [config_default.fsx](/src/FsOpenAI.Tasks/deployments/default/config_default.fsx) script contains an example of how to create a samples file in a type-safe way.

### 5. Authentication Configuration

The FsOpenAI app supports integration with Azure AD (or MS Entra ID). To require authentication, set the *RequireLogin* flag in the AppConfig (see above). The user roles can also be specified there. See [MS authentication for Blazor apps](https://learn.microsoft.com/en-us/azure/active-directory/develop/tutorial-blazor-webassembly) for more information. 

There are two places additional configuration is required to support AD authentication:

- (client) wwwroot/appsettings.json
- (server) appsettings.json

Please review the MS documentation for details. This is an involved topic which requires good study and some trial-and-error for successful execution.

Note *appsettings.json* files are not checked in to the repo as they may contain proprietary data. Instead make a copy of the appsettings.template.json files to appesttings.json in the corresponding locations and add the required configuration.

## Search Index Format
FsOpenAI expects the following fields to be present in each configured Azure AI Search index. If any field is missing, it will not show the index in the Q&A chat *index selection box*. Any additional fields are ignored. 

|Field|Description|
|-----|-----------|
|id|Unique id|
|title|Document title|
|sourcefile|Link to reference site or document|
|content|The text content that will be used in the Q&A chat session|
|contentVector|The embeddings vector

Sample code to 'shred' a collection of PDF documents; create the embeddings; and load the index, is provided in the script [LoadIndex.fsx](/src/FsOpenAI.Tasks/scripts/LoadIndex.fsx).


*Note the index format may change to the [Semantic Kernel 'memory' format later.](https://learn.microsoft.com/en-us/semantic-kernel/get-started/quick-start-guide/memory-and-embeddings?tabs=Csharp)*

The api to create embeddings may be throttled (by Azure / OpenAI). The index loading code allows for rate limiting on the client/caller side to facilitate smoother batch index load operation of a large document collections.

### Meta Index 
In addition to the actual document indexes a special index, called 'Meta' index here, may also be created. The name of the Meta index - that an app deployment should use - is configured in the AppConfig.json (see configuration section above). The Meta index specifies the following additional information about the actual indexes:

- Descriptions of the actual indexes that will display in the app where indexes can be selected.
- The 'groups' each of the actual indexes are associated with. In the AppConfig.json is a list of groups for the deployment; the app will show only the indexes where the groups match between the Meta index and AppConfig.json.
- Parent indexes (if any) for each of the actual indexes. This will show the indexes in a hierarchy in the app. If a parent is selected (in the app) then the children are automatically selected.
- Virtual indexes. These are used for organizing the index hierarchy. The application does not expect an actual document index to exist in the Azure AI Search service, if it is marked as 'isVirtual' in the Meta index.

If the Meta index is missing (or the one named in the AppConfig.json is not found), then all indexes (in the referenced Azure AI Search instance) containing the required columns, are shown in the app -  as a flat list (i.e. not in a hierarchy).

If a Meta index is found, then only the actual indexes listed in the Meta index are shown to the app (respective of the groups).

The sample code to create a Meta index is in [config_default.fsx](/src/FsOpenAI.Tasks/deployments/default/config_default.fsx)


# Local Testing And Development

For local testing, the settings json file can be specified in (server) *appsettings.json*. By default its configured to look for a file with the path:

-  (HOME directory)/.fsopenai/ServiceSettings.json

Note HOME is %USERPROFILE% on Windows and %HOME% on Linux and Macos.

Also the OpenAI API key can be specified in the FsOpenAI UI via settings. The key is stored locally in the brower local storage.
