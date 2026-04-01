# Azure AI Foundry Setup

## Table of Contents

- [Create a project in Azure AI Foundry](#create-a-project-in-azure-ai-foundry)
- [Deploy the models](#deploy-the-models)
- [Get the endpoint and API key](#get-the-endpoint-and-api-key)
- [Authentication options](#authentication-options)

---

## Create a project in Azure AI Foundry

1. Go to [Azure AI Foundry](https://ai.azure.com) and sign in.
2. Create a new **Hub** (or use an existing one) and create a **Project** inside it.
3. Note the **Project endpoint URL** shown on the project overview page (format: `https://<project>.services.ai.azure.com/api/projects/<project>`).

Alternatively, you can use a plain **Azure OpenAI** resource from the [Azure portal](https://portal.azure.com). The app supports both endpoint formats.

---

## Deploy the models

Inside your Azure AI Foundry project (or Azure OpenAI resource), deploy:

| Model | Suggested deployment name |
|---|---|
| `whisper-large-v3` | `whisper` |
| `gpt-4o-mini` (or `gpt-4o`) | `gpt-4o-mini` |

Steps:
1. Open **Model catalog** in AI Foundry (or **Model deployments** in Azure OpenAI Studio).
2. Search for `whisper-large-v3` and click **Deploy**. Note the deployment name.
3. Repeat for `gpt-4o-mini`.

> The default Azure Whisper quota is **3 requests per minute**. The app sends at most 2 requests per minute (one chunk every 30 s), which fits within this limit.

---

## Get the endpoint and API key

- **Endpoint** – found on the project overview page in AI Foundry, or the resource overview in the Azure portal.  
  Supported formats:
  - `https://<resource>.openai.azure.com/`
  - `https://<resource>.cognitiveservices.azure.com/`
  - `https://<project>.services.ai.azure.com/api/projects/<project>`

- **API Key** – found under **Keys and Endpoint** on the resource in the Azure portal, or under **Settings → API keys** in AI Foundry.

---

## Authentication options

The app supports three authentication methods:

| Method | When to use |
|---|---|
| API key | Quickest option; store the key in the Settings UI or `appsettings.json` |
| `az login` (DefaultAzureCredential) | Leave the API key blank; the app uses the Azure CLI login on your machine |
| Managed identity / environment variables | Leave the API key blank; `DefaultAzureCredential` picks up any supported credential automatically |

For the passwordless options, assign the **Cognitive Services User** role (or **Azure AI Developer** for AI Foundry projects) to your account or managed identity on the resource.

See also: [Azure AI Foundry Whisper Quickstart](https://learn.microsoft.com/azure/foundry/openai/whisper-quickstart)
