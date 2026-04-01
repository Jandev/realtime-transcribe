# Configuration

## Table of Contents

- [Option A – Settings UI](#option-a--settings-ui)
- [Option B – appsettings.json](#option-b--appsettingsjson)
- [Option C – .NET User Secrets](#option-c--net-user-secrets)
- [Option D – DefaultAzureCredential (passwordless)](#option-d--defaultazurecredential-passwordless)
- [Configuration reference](#configuration-reference)

---

## Option A – Settings UI

Run the app and open the **Settings** tab. Enter the values and click **Save**. Settings are persisted across app restarts.

---

## Option B – appsettings.json

Edit `src/RealtimeTranscribe/appsettings.json` before building:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "<your-api-key>",
    "WhisperDeploymentName": "whisper",
    "ChatDeploymentName": "gpt-4o-mini"
  }
}
```

> **Warning:** Do not commit API keys to source control. Use User Secrets (Option C) or leave `ApiKey` empty and use passwordless auth (Option D) instead.

---

## Option C – .NET User Secrets

User Secrets keep credentials out of source control:

```bash
cd src/RealtimeTranscribe
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-api-key>"
dotnet user-secrets set "AzureOpenAI:WhisperDeploymentName" "whisper"
dotnet user-secrets set "AzureOpenAI:ChatDeploymentName" "gpt-4o-mini"
```

---

## Option D – DefaultAzureCredential (passwordless)

Leave **ApiKey** blank (empty string or omitted). The app uses `DefaultAzureCredential`, which automatically tries, in order: environment variables, workload identity, managed identity, Azure CLI (`az login`), and more.

To authenticate via the Azure CLI:

```bash
az login
```

Make sure the account has the **Cognitive Services User** role (or **Azure AI Developer** for AI Foundry projects) on the resource.

---

## Configuration reference

| Key | Default | Description |
|---|---|---|
| `AzureOpenAI:Endpoint` | *(empty)* | Azure OpenAI or AI Foundry endpoint URL |
| `AzureOpenAI:ApiKey` | *(empty)* | API key; leave blank for DefaultAzureCredential |
| `AzureOpenAI:WhisperDeploymentName` | `whisper` | Name of the Whisper model deployment |
| `AzureOpenAI:ChatDeploymentName` | `gpt-4o-mini` | Name of the chat model deployment |

Settings configured via the Settings UI (Option A) take precedence over `appsettings.json` values at runtime.
