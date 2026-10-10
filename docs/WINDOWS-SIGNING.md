# Windows EXE 签名

FlyPet 当前没有可用的代码签名证书，因此本地和 GitHub 上现有的 EXE 仍是未签名状态。自签名证书只适合内部测试，普通用户的 Windows 不会默认信任它，不能解决 SmartScreen 提示。

## 推荐方案：Microsoft Artifact Signing

微软把 Artifact Signing 列为 Windows 应用的首选签名方式。它保管证书私钥，并支持 GitHub Actions 使用 OIDC 获取短期身份，不需要把 PFX 私钥上传到仓库。

但 Public Trust 有地区限制：微软目前只接受位于美国、加拿大、欧盟、英国、澳大利亚、新西兰、日本、韩国、新加坡、瑞士、挪威和以色列的组织；个人开发者仅限美国或加拿大。中国大陆的个人开发者通常无法申请这个公开信任配置，应选择下文的传统 OV 代码签名证书。Private Trust 不受这个地区限制，但普通用户的 Windows 不会默认信任它，因此不适合公开下载的软件。

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

也可以向受 Windows 信任、且支持申请人所在地区的证书机构购买 OV/EV 代码签名证书，再用 Windows SDK 的 `signtool.exe` 签名。申请时通常需要真实姓名或企业登记信息、地址和联系方式，证书显示的发布者会是验证后的法定名称。现代公开信任代码签名证书通常要求把私钥保存在硬件令牌或云 HSM 中，因此 GitHub Actions 的接入方式取决于证书供应商；不能假定一定会拿到一个可上传的 PFX 文件。

不要把身份证件、私钥、令牌 PIN 或证书密码提交到仓库或发在聊天中。确定供应商后，只需告诉维护者它提供的是硬件令牌、云签名服务还是可导出的 PFX；对应凭据应由仓库所有者直接写入 GitHub Secrets。

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
