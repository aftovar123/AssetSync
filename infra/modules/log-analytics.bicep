// The Log Analytics workspace behind Application Insights. It lives in
// Azure's default resource group for the region (created by the portal), so
// main.bicep deploys this module scoped to that group.

param name string
param location string

@description('Daily ingestion cap in GB. Keeps the workspace inside the free allowance.')
param dailyQuotaGb string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json(dailyQuotaGb)
    }
  }
}

output id string = workspace.id
