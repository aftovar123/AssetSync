// Everything AssetSync runs on in Azure, as it exists today: free-tier App
// Service, serverless Azure SQL on the free offer, Application Insights over
// a capped Log Analytics workspace, and the managed identity GitHub Actions
// uses to deploy. Deploy with infra/deploy.sh, which previews the changes
// (what-if) before applying them.

targetScope = 'resourceGroup'

param location string = resourceGroup().location

param appServicePlanName string
param webAppName string
param sqlServerName string
param sqlDatabaseName string
param appInsightsName string
param deployIdentityName string

param workspaceName string
param workspaceResourceGroup string
param workspaceDailyQuotaGb string

param sqlAdminLogin string

@secure()
@description('Only needed when the SQL server is created; leave empty to keep the current password.')
param sqlAdminPassword string = ''

@description('GitHub repository allowed to deploy, in the subject format GitHub uses for OIDC tokens.')
param githubOidcSubject string

@description('Existing role assignment name, so redeploying updates it instead of creating a duplicate.')
param deployRoleAssignmentName string

param authClients array

@secure()
param sqlConnectionString string
@secure()
param jwtSigningKey string
@secure()
param rabbitMqConnectionString string
@secure()
@description('Client secrets keyed by client id.')
param authClientSecrets object

// ---------- Monitoring ----------

module workspace 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  scope: resourceGroup(workspaceResourceGroup)
  params: {
    name: workspaceName
    location: location
    dailyQuotaGb: workspaceDailyQuotaGb
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.outputs.id
    IngestionMode: 'LogAnalytics'
    RetentionInDays: 90
  }
}

// ---------- Database ----------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: empty(sqlAdminPassword) ? null : sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// 0.0.0.0 is Azure's convention for "allow other Azure services", which is
// how the App Service reaches the database.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Serverless on the free offer: pauses after an hour idle, and pauses
// instead of billing if the monthly free allowance runs out.
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 34359738368
    autoPauseDelay: 60
    minCapacity: json('0.5')
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

// ---------- App Service ----------

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true
  }
}

var authClientSettings = reduce(range(0, length(authClients)), {}, (acc, i) => union(acc, {
  'Auth__Clients__${i}__ClientId': authClients[i].clientId
  'Auth__Clients__${i}__Scopes': authClients[i].scopes
  'Auth__Clients__${i}__ClientSecret': authClientSecrets[authClients[i].clientId]
}))

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  tags: {
    'hidden-link: /app-insights-resource-id': appInsights.id
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appCommandLine: 'dotnet AssetSync.Api.dll'
      alwaysOn: false // not available on the free plan
      use32BitWorkerProcess: true
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      http20Enabled: false
    }
  }
}

// The whole settings list is replaced on every deploy, so every setting the
// app needs is declared here. The App Insights connection string comes from
// the resource itself instead of being pasted by hand.
resource appSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: webApp
  name: 'appsettings'
  properties: union({
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
    Jwt__SigningKey: jwtSigningKey
    RabbitMq__ConnectionString: rabbitMqConnectionString
  }, authClientSettings)
}

resource connectionStrings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: webApp
  name: 'connectionstrings'
  properties: {
    AssetSyncDb: {
      value: sqlConnectionString
      type: 'SQLAzure'
    }
  }
}

// ---------- Deployment identity (GitHub Actions OIDC) ----------

resource deployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: deployIdentityName
  location: location
}

resource githubFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: deployIdentity
  name: 'oidc-credential-aff9'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: githubOidcSubject
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

// Website Contributor on the web app only: the pipeline can deploy code and
// nothing else in the subscription.
resource deployRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: deployRoleAssignmentName
  scope: webApp
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'de139f84-1756-47ae-9be6-808fbbe84772')
    principalId: deployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
