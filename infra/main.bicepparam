using 'main.bicep'

// Names and settings that are not secret. Secrets are passed by deploy.sh.

param appServicePlanName = 'ASP-assetsyncrg-8774'
param webAppName = 'assetsync-api-andres'
param sqlServerName = 'assetsync-server-andres'
param sqlDatabaseName = 'assetsyncdb'
param appInsightsName = 'assetsync-insights'
param deployIdentityName = 'oidc-msi-90b6'

param workspaceName = 'DefaultWorkspace-62a02b56-d1dd-4fd3-8540-77fbfb6a1170-USW3'
param workspaceResourceGroup = 'DefaultResourceGroup-USW3'
param workspaceDailyQuotaGb = '0.1'

param sqlAdminLogin = 'assetsyncadmin'

param githubOidcSubject = 'repo:aftovar123@49291862/AssetSync@1381559198:ref:refs/heads/main'
param deployRoleAssignmentName = '24ba19de-32ac-4b70-bfaa-055e54987cdf'

param authClients = [
  {
    clientId: 'erp-integration'
    scopes: 'workorders.write integration.read'
  }
  {
    clientId: 'asset-admin'
    scopes: 'assets.write'
  }
]

// Supplied at deploy time; see deploy.sh.
param sqlConnectionString = readEnvironmentVariable('ASSETSYNC_SQL_CONNECTION_STRING', '')
param jwtSigningKey = readEnvironmentVariable('ASSETSYNC_JWT_SIGNING_KEY', '')
param rabbitMqConnectionString = readEnvironmentVariable('ASSETSYNC_RABBITMQ_CONNECTION_STRING', '')
param authClientSecrets = {
  'erp-integration': readEnvironmentVariable('ASSETSYNC_CLIENT_ERP_INTEGRATION_SECRET', '')
  'asset-admin': readEnvironmentVariable('ASSETSYNC_CLIENT_ASSET_ADMIN_SECRET', '')
}
