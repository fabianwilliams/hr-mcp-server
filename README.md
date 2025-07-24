# HR MCP Server - Azure Container Apps Deployment

A Microsoft HR MCP (Model Context Protocol) server deployed to Azure Container Apps with HTTP streaming support and Azure Table Storage persistence. This server provides HR candidate management tools for AI assistants like Claude and Copilot Studio.

## 🙏 Inspiration & Credit

This project builds upon the excellent foundational work by [**Paolo Pialorsi**](https://github.com/PaoloPia), a Microsoft Developer Advocate. His original **Lab 6 - MCP Server** from the Copilot Camp series demonstrated how to expose a simple, in-memory MCP server via HTTP for learning and experimentation purposes.

📚 View the original lab here:  
https://microsoft.github.io/copilot-camp/pages/make/copilot-studio/06-mcp/

While Paolo's version was tailored for educational use, this project extends his concept by:
- Deploying the MCP server fully remote to Azure Container Apps
- Replacing in-memory lists with persistent Azure Table Storage
- Enabling streaming and concurrent access for real-world scenarios

Kudos to Paolo for providing a solid springboard and inspiration for this work.

---

## 🚀 Live Server

**Production URL**: https://hr-mcp-server.jollyflower-9d7ab707.eastus2.azurecontainerapps.io  
**Status**: ✅ **Working with persistent storage!** - Azure Table Storage integration completed

![Claude Desktop Working](Images/claude-desktop-working.png)

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
- **Azure Table Storage** - Persistent candidate data storage  
- **Azure Container Registry** - Private container image storage  
- **GitHub Actions** - CI/CD pipeline

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐  
│   Human /AI     │───▶│  HR MCP Server   │───▶│ Azure Table     │  
│   Agent         │    │ (Container Apps) │    │   Storage       │  
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

**Note**: For local development, set the `TABLE_STORAGE_CONN_STRING` environment variable with your Azure Storage connection string.

## ☁️ Azure Deployment Journey

### Key Features Implemented

✅ **Azure Table Storage Persistence** - Candidate data survives container restarts  
✅ **Concurrent Connection Support** - Multiple clients can connect simultaneously  
✅ **Automatic Data Seeding** - Initial candidate data loaded from JSON file  
✅ **CI/CD Pipeline** - Automated deployments via GitHub Actions  
✅ **Production Ready** - Proper error handling and logging

### Deployment Process

#### Initial Setup

```bash
az group create --name mcpserversalpha --location eastus2
az containerapp env create --name mcp-env --resource-group mcpserversalpha --location eastus2
```

## 🔧 Major Technical Solutions

### 1. Azure Table Storage Integration

**Problem**: In-memory storage caused data loss on container restarts and poor concurrent connection handling.

**Solution**:
- `CandidateTableEntity` - Maps candidate data to Table Storage
- `TableStorageCandidateService` - Persistent service layer
- `DataSeedingService` - Loads data from JSON if empty
- Async support for all reads/writes

**Schema**:
```
Table: "Candidates"
PartitionKey: "Candidate"
RowKey: candidate.Email
Properties: FirstName, LastName, Skills, SpokenLanguages, CurrentRole
```

### 2. .NET Framework Compatibility

**Problem**: Targeted .NET 10.0, but Container Registry supports up to .NET 8.0.  
**Fix**:
```xml
<TargetFramework>net8.0</TargetFramework>
```

### 3. Container Registry Authentication

**Fix**:
- Enabled system-managed identity  
- Assigned `AcrPull` role  
- Configured registry pull via Azure CLI

### 4. Port Configuration

**Fix**:
```bash
az containerapp ingress update --name hr-mcp-server --resource-group mcpserversalpha --target-port 8080
```

### 5. Claude Desktop Configuration

**Fix**: Use `mcp-remote` with direct HTTP URL instead of `@modelcontextprotocol/server-http`.

```json
{
  "command": "npx",
  "args": ["-y", "mcp-remote", "https://your-mcp-server-url"]
}
```

## 🔍 Environment Configuration

### Environment Variables

- `TABLE_STORAGE_CONN_STRING` - Required  
- `ConnectionStrings__TableStorage` - Optional alternative config key

## 🔍 Debugging Tips

### Check Active Revisions

```bash
az containerapp revision list --name hr-mcp-server --resource-group mcpserversalpha 
  --query "[].{name:name,active:properties.active,image:properties.template.containers[0].image}" -o table
```

### View Logs

```bash
az containerapp logs show --name hr-mcp-server --resource-group mcpserversalpha --follow false --tail 10
```

### Test MCP Endpoint

```bash
curl -X POST https://hr-mcp-server.jollyflower-9d7ab707.eastus2.azurecontainerapps.io/ 
  -H "Content-Type: application/json" 
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
```

**Expected Response**:
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

- Auto-scaling: 0-10 replicas  
- Cold start: ~2-3 sec  
- Latency: <100ms  
- Concurrent support: Yes  
- Persistence: Yes (Table Storage)

## 🛡️ Security

- TLS 1.3 (HTTPS)  
- Managed Identity for ACR  
- Secrets via environment vars  
- No secrets in source code

## 📈 Monitoring

- Application Insights telemetry  
- Container metrics  
- MCP session logging  
- Storage operation metrics

## 🤝 Contributing

1. Fork this repo  
2. Create a feature branch  
3. Test using Claude or Inspector  
4. Submit PR  

## 📄 License

This builds on Microsoft MCP samples. See LICENSE for terms.

## 🆘 Support

- Deployment issues → see above  
- MCP spec → https://modelcontextprotocol.io  
- Azure services → Azure Docs

---

**Deployed**: July 24, 2025  
**Maintainer**: Fabian Williams  
**Status**: ✅ Production ready  
