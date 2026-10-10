# Windows EXE 签名

FlyPet 当前没有可用的代码签名证书，因此本地和 GitHub 上现有的 EXE 仍是未签名状态。自签名证书只适合内部测试，普通用户的 Windows 不会默认信任它，不能解决 SmartScreen 提示。

## 推荐方案：Microsoft Artifact Signing

微软把 Artifact Signing 列为 Windows 应用的首选签名方式。它保管证书私钥，并支持 GitHub Actions 使用 OIDC 获取短期身份，不需要把 PFX 私钥上传到仓库。

启用步骤：

1. 在 Azure 创建 Artifact Signing 账户，完成身份验证，并创建代码签名证书配置文件。
2. 创建 Entra 应用和 GitHub 仓库的联合身份凭据，为它授予 `Artifact Signing Certificate Profile Signer` 角色。
3. 在 GitHub 仓库 Secrets 中添加：
   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
4. 在 GitHub 仓库 Variables 中添加：
   - `AZURE_ARTIFACT_SIGNING_ENDPOINT`，例如 `https://eus.codesigning.azure.net/`
   - `AZURE_ARTIFACT_SIGNING_ACCOUNT`
   - `AZURE_ARTIFACT_SIGNING_PROFILE`
5. 推送新版本标签。发布工作流会签名 `FlyPet.exe`，使用 SHA-256 和微软 RFC 3161 时间戳，验证签名有效后再生成 ZIP。

没有配置 `AZURE_ARTIFACT_SIGNING_ENDPOINT` 时，签名步骤会自动跳过，普通构建和自测不受影响。

## 传统证书方案

也可以向受 Windows 信任的证书机构购买 OV/EV 代码签名证书，再用 Windows SDK 的 `signtool.exe` 签名。若证书是 PFX，私钥和密码只能存入 GitHub Secrets；若证书位于硬件令牌或云 HSM，则需要使用供应商提供的 GitHub Actions 集成或自托管 Windows runner。

无论哪种方式，都应在压缩 ZIP 之前执行签名，并使用 SHA-256 文件摘要和 RFC 3161 时间戳。签名后可在 PowerShell 中检查：

```powershell
Get-AuthenticodeSignature .\FlyPet.exe | Format-List Status,SignerCertificate,TimeStamperCertificate
```

只有 `Status` 为 `Valid` 才应上传发布包。

## 官方资料

- [Windows Smart App Control 代码签名说明](https://learn.microsoft.com/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control)
- [Microsoft Artifact Signing 集成说明](https://learn.microsoft.com/azure/artifact-signing/how-to-signing-integrations)
- [Azure Artifact Signing GitHub Action](https://github.com/Azure/artifact-signing-action)
- [SignTool 官方文档](https://learn.microsoft.com/windows/win32/seccrypto/signtool)
