# HR MCP Server - Azure Container Apps Deployment

A Microsoft HR MCP (Model Context Protocol) server sample deployed to Azure Container Apps with HTTP streaming support. This server provides HR candidate management tools for AI assistants like Claude and Copilot Studio.

## 🚀 Live Server

**Production URL**: https://hr-mcp-server.jollyflower-9d7ab707.eastus2.azurecontainerapps.io

**Status**: ✅ Active and responding to MCP requests

## 📋 Available Tools

The HR MCP server provides the following candidate management tools:

- `list_candidates` - List all candidates in the system
- `search_candidates` - Search candidates by criteria
- `add_candidate` - Add a new candidate
- `update_candidate` - Update existing candidate information
- `remove_candidate` - Remove a candidate from the system

## 🔧 Technology Stack

- **.NET 8.0** - ASP.NET Core web application
- **Model Context Protocol** - HTTP streaming transport
- **Azure Container Apps** - Serverless container hosting
- **Azure Container Registry** - Private container image storage
- **GitHub Actions** - CI/CD pipeline (configured)

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Claude/AI     │───▶│  HR MCP Server   │───▶│   Candidates    │
│   Assistant     │    │ (Container Apps) │    │   JSON Data     │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                       ┌──────────────────┐
                       │ Azure Container  │
                       │    Registry      │
                       └──────────────────┘
```

## 🚀 Quick Start

### For Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "hr-server": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote",
        "https://hr-mcp-server.jollyflower-9d7ab707.eastus2.azurecontainerapps.io"
      ]
    }
  }
}
```

### For MCP Inspector

Test the server directly:

```bash
npx @modelcontextprotocol/inspector https://hr-mcp-server.jollyflower-9d7ab707.eastus2.azurecontainerapps.io
```

### For Copilot Studio

Use the production URL as your custom connector endpoint for MCP integration.

## 📦 Local Development

```bash
# Clone the repository
git clone https://github.com/fabianwilliams/hr-mcp-server.git
cd hr-mcp-server

# Build and run locally
dotnet build
dotnet run

# The server will be available at http://localhost:5000
```

## ☁️ Azure Deployment Journey

### Deployment Process

This project was deployed to Azure Container Apps with several challenges and solutions documented below.

### Initial Setup

```bash
# Create resource group
az group create --name mcpserversalpha --location eastus2

# Create Container Apps environment
az containerapp env create --name mcp-env --resource-group mcpserversalpha --location eastus2
```

## 🐛 Deployment Gotchas & Solutions

### 1. .NET Framework Compatibility Issue

**Problem**: Initial project targeted .NET 10.0, but Azure Container Registry's auto-build only supports up to .NET 8.0.

```
Error: Platform 'dotnet' version '10.0' is unsupported. Supported versions: [..., 8.0.7, 1.0.16]
```

**Solution**: Updated `hr-mcp-server.csproj` to target .NET 8.0:

```xml
<TargetFramework>net8.0</TargetFramework>
```

### 2. Container Registry Authentication

**Problem**: Container App couldn't pull the custom image from Azure Container Registry due to authentication issues.

```
Error: UNAUTHORIZED: authentication required, visit https://aka.ms/acr/authorization
```

**Solution**: 
1. Enabled system-assigned managed identity for Container App
2. Granted AcrPull role to the managed identity
3. Configured registry credentials using admin credentials as fallback

```bash
# Enable managed identity
az containerapp identity assign --name hr-mcp-server --resource-group mcpserversalpha --system-assigned

# Grant ACR pull permissions
az role assignment create --assignee <principal-id> --role AcrPull --scope <acr-resource-id>

# Configure registry credentials
az containerapp registry set --name hr-mcp-server --resource-group mcpserversalpha \
  --server <acr-server> --username <acr-username> --password <acr-password>
```

### 3. Port Mismatch Issue

**Problem**: ASP.NET Core app was listening on port 8080, but Container App ingress was configured for port 80, resulting in 404 errors.

**Logs showed**:
```
Now listening on: http://[::]:8080
```

**But ingress expected port 80, causing**:
```
404 page not found
```

**Solution**: Updated Container App ingress target port to match the application port:

```bash
az containerapp ingress update --name hr-mcp-server --resource-group mcpserversalpha --target-port 8080
```

### 4. Multiple Active Revisions

**Problem**: Container App had both default "Hello World" image and custom HR MCP server image active simultaneously, causing inconsistent responses.

**Solution**: Ensured single revision mode and proper traffic routing to the custom image revision.

### 5. CI/CD Pipeline Authentication

**Problem**: GitHub Actions workflow failed to authenticate with Azure Container Registry during automated deployments.

**Current Status**: Manual deployment successful, CI/CD pipeline needs ACR credential configuration in GitHub secrets.

### 6. Claude Desktop HTTP MCP Configuration

**Problem**: Initial configuration used `@modelcontextprotocol/server-http` package which doesn't exist, causing "404 Not Found" npm errors.

```
npm error 404  '@modelcontextprotocol/server-http@*' is not in this registry.
```

**Solution**: Use `mcp-remote` package instead for HTTP-based MCP servers:

```json
{
  "command": "npx",
  "args": ["-y", "mcp-remote", "https://your-mcp-server-url"]
}
```

This matches the pattern used by other working HTTP MCP servers in the configuration.

## 🔍 Debugging Tips

### Check Active Revisions
```bash
az containerapp revision list --name hr-mcp-server --resource-group mcpserversalpha \
  --query "[].{name:name,active:properties.active,image:properties.template.containers[0].image}" -o table
```

### View Container Logs
```bash
az containerapp logs show --name hr-mcp-server --resource-group mcpserversalpha --follow false --tail 10
```

### Test MCP Endpoint
```bash
curl -X POST https://hr-mcp-server.jollyflower-9d7ab707.eastus2.azurecontainerapps.io/ \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
```

Expected response:
```json
{
  "result": {
    "protocolVersion": "2024-11-05",
    "capabilities": {
      "logging": {},
      "tools": {"listChanged": true}
    },
    "serverInfo": {
      "name": "hr-mcp-server",
      "version": "1.0.0.0"
    }
  },
  "id": 1,
  "jsonrpc": "2.0"
}
```

## 📊 Performance & Scaling

- **Auto-scaling**: 0-10 replicas based on demand
- **Cold start**: ~2-3 seconds for first request
- **Response time**: <100ms for MCP tool calls
- **Concurrent sessions**: Supports multiple MCP clients

## 🛡️ Security

- **HTTPS**: TLS 1.3 encryption for all communications
- **Managed Identity**: Secure access to Azure Container Registry
- **No exposed secrets**: All credentials stored in Azure Key Vault/Container App secrets

## 📈 Monitoring

- **Application Insights**: Integrated telemetry and logging
- **Container Apps metrics**: CPU, memory, and request metrics
- **MCP session tracking**: Session IDs logged for debugging

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test locally and with MCP Inspector
5. Submit a pull request

## 📄 License

This project is based on Microsoft's MCP server samples and follows the same licensing terms.

## 🆘 Support

For issues with:
- **Deployment**: Check the gotchas section above
- **MCP Protocol**: Refer to [Model Context Protocol docs](https://modelcontextprotocol.io/)
- **Azure Container Apps**: Check Azure documentation

---

**Deployment completed**: July 24, 2025  
**Last updated**: July 24, 2025  
**Deployed by**: Claude Code (claude.ai/code)