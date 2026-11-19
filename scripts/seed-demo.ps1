#requires -Version 7.0
[CmdletBinding()]
param(
    [uri]$ApiBaseUrl = 'http://localhost:5109/api/v1',
    [pscredential]$Credential
)

$ErrorActionPreference = 'Stop'
if (-not $ApiBaseUrl.IsAbsoluteUri -or $ApiBaseUrl.UserInfo -or $ApiBaseUrl.Query -or $ApiBaseUrl.Fragment -or
    ($ApiBaseUrl.Scheme -ne 'https' -and ($ApiBaseUrl.Scheme -ne 'http' -or -not $ApiBaseUrl.IsLoopback))) {
    throw 'Use an HTTPS API base URL, or HTTP on loopback, without credentials, a query, or a fragment.'
}
$baseUrl = $ApiBaseUrl.AbsoluteUri.TrimEnd('/')

function Send-DemoPost {
    param([string]$Path, [hashtable]$Body, [hashtable]$Headers = @{})

    Invoke-RestMethod -Method Post -Uri "$baseUrl$Path" -Headers $Headers -ContentType 'application/json' `
        -Body ($Body | ConvertTo-Json -Compress) -TimeoutSec 30 -MaximumRedirection 0
}

if (-not $Credential) {
    $Credential = Get-Credential -UserName 'admin@example.test' -Message 'Enter the provisioned demo Admin credentials.'
}
if (-not $Credential) { throw 'Admin credentials are required to seed the demo.' }

$session = Send-DemoPost -Path '/auth/login' -Body @{
    email = $Credential.UserName
    password = $Credential.GetNetworkCredential().Password
}
$headers = @{ Authorization = "Bearer $($session.accessToken)" }
$runId = [guid]::NewGuid().ToString('N').Substring(0, 12)
$reference = "DEMO-$($runId.ToUpperInvariant())"
$today = [DateTime]::UtcNow.Date
$dateFormat = 'yyyy-MM-dd'
$culture = [Globalization.CultureInfo]::InvariantCulture

$client = Send-DemoPost '/clients' @{
    name = 'Alex Example'
    email = "alex-$runId@example.test"
} $headers

$commonMatter = @{
    primaryClientId = $client.id
    openedDate = $today.AddDays(-14).ToString($dateFormat, $culture)
}
$lease = Send-DemoPost '/matters' ($commonMatter + @{
    referenceNumber = "$reference-LEASE"
    title = 'Maple Street lease review'
    description = 'Fictional review of a commercial lease and its supporting paperwork.'
    status = 'InProgress'
    priority = 'High'
}) $headers
$agreement = Send-DemoPost '/matters' ($commonMatter + @{
    referenceNumber = "$reference-AGREEMENT"
    title = 'Harbor Studio services agreement'
    description = 'Fictional preparation of a services agreement for a design studio.'
    status = 'Open'
    priority = 'Normal'
    targetCloseDate = $today.AddDays(14).ToString($dateFormat, $culture)
}) $headers

$task = Send-DemoPost "/matters/$($lease.id)/tasks" @{
    title = 'Review lease checklist'
    description = 'Compare the fictional lease against the intake checklist.'
    dueDate = $today.AddDays(-2).ToString($dateFormat, $culture)
} $headers
$deadline = Send-DemoPost "/matters/$($lease.id)/deadlines" @{
    title = 'Send lease comments'
    date = $today.AddDays(-1).ToString($dateFormat, $culture)
} $headers

[pscustomobject]@{
    ClientId = $client.id
    RecipientEmail = $client.email
    LeaseMatterId = $lease.id
    LeaseReference = $lease.referenceNumber
    AgreementMatterId = $agreement.id
    AgreementReference = $agreement.referenceNumber
    OverdueTaskId = $task.id
    OverdueDeadlineId = $deadline.id
}
