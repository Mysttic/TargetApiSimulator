<#
.SYNOPSIS
    Checks a running TargetApiSimulator against its documented contract.

.DESCRIPTION
    Start the simulator first, then point this script at it. Every check prints PASS or FAIL and
    the script exits with 1 if anything failed, so it can be used as a CI gate.

.EXAMPLE
    .\TargetApiSimulator.exe --urls http://localhost:5000
    .\smoke-test.ps1

.EXAMPLE
    .\smoke-test.ps1 -BaseUrl http://localhost:8080
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5000'
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

Add-Type -AssemblyName System.Net.Http | Out-Null
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)

$script:Passed = 0
$script:Failed = 0

function Send-Request {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Body,
        [string]$ContentType = 'application/json'
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method), "$BaseUrl$Path")

    if ($PSBoundParameters.ContainsKey('Body')) {
        $request.Content = [System.Net.Http.StringContent]::new(
            $Body, [System.Text.Encoding]::UTF8, $ContentType)
    }

    $response = $client.SendAsync($request).GetAwaiter().GetResult()

    [pscustomobject]@{
        Status      = [int]$response.StatusCode
        Body        = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        ContentType = if ($response.Content.Headers.ContentType) {
            $response.Content.Headers.ContentType.ToString()
        } else { $null }
        Allow       = ($response.Content.Headers.Allow -join ',')
    }
}

function Assert-That {
    param([string]$Name, [string]$Expected, [string]$Actual)

    if ($Expected -eq $Actual) {
        Write-Host ('  PASS  {0}' -f $Name) -ForegroundColor Green
        $script:Passed++
    }
    else {
        Write-Host ('  FAIL  {0}' -f $Name) -ForegroundColor Red
        Write-Host ('        expected: {0}' -f $Expected) -ForegroundColor DarkGray
        Write-Host ('        actual:   {0}' -f $Actual) -ForegroundColor DarkGray
        $script:Failed++
    }
}

Write-Host ''
Write-Host "TargetApiSimulator smoke test -> $BaseUrl" -ForegroundColor Cyan
Write-Host ''

try {
    $null = Send-Request -Method GET -Path '/healthz'
}
catch {
    Write-Host "Cannot reach $BaseUrl - is the simulator running?" -ForegroundColor Red
    Write-Host "Start it with: .\TargetApiSimulator.exe --urls $BaseUrl" -ForegroundColor DarkGray
    exit 2
}

Write-Host 'Service endpoints'
$r = Send-Request -Method GET -Path '/healthz'
Assert-That 'GET /healthz returns 200'            '200' $r.Status
Assert-That 'GET /healthz body'                   '{"status":"ok"}' $r.Body

$r = Send-Request -Method GET -Path '/version'
Assert-That 'GET /version returns 200'            '200' $r.Status
Assert-That 'GET /version reports a version'      'True' ([string]($r.Body -match '"version":"[^"]+"'))
Write-Host ('        version: {0}' -f $r.Body) -ForegroundColor DarkGray

Write-Host ''
Write-Host 'Valid payloads'
foreach ($payload in '{"key":"value"}', '[1,2,3]', '{}', '[]', '123', '"str"', 'true', 'null') {
    $r = Send-Request -Method POST -Path '/api/target' -Body $payload
    Assert-That ('POST {0} -> 200 true' -f $payload) '200 true' ('{0} {1}' -f $r.Status, $r.Body)
}

Write-Host ''
Write-Host 'Rejected payloads'
foreach ($payload in 'not json at all', '{"a":}', '{"a":1,}', '{"a":1} // c', '{', '') {
    $label = if ($payload -eq '') { '(empty body)' } else { $payload }
    $r = Send-Request -Method POST -Path '/api/target' -Body $payload
    Assert-That ('POST {0} -> 400' -f $label) `
        '400 {"ErrorMessage": "This is not JSON"}' ('{0} {1}' -f $r.Status, $r.Body)
}

Write-Host ''
Write-Host 'Contract details'
$r = Send-Request -Method POST -Path '/api/target' -Body '{"a":1}'
Assert-That 'Response declares JSON content type' 'application/json; charset=utf-8' $r.ContentType

$deep = ('[' * 65) + (']' * 65)
$r = Send-Request -Method POST -Path '/api/target' -Body $deep
Assert-That 'Nesting deeper than 64 -> 400'       '400' $r.Status

$r = Send-Request -Method POST -Path '/api/target' -Body '{"a":1}' -ContentType 'text/plain'
Assert-That 'Content-Type is ignored -> 200'      '200' $r.Status

$r = Send-Request -Method GET -Path '/api/target'
Assert-That 'GET /api/target -> 405'              '405' $r.Status
Assert-That '405 advertises Allow: POST'          'POST' $r.Allow

$r = Send-Request -Method POST -Path '/does-not-exist' -Body '{}'
Assert-That 'Unknown path -> 404'                 '404' $r.Status

$big = '{"a":"' + ('x' * 1100000) + '"}'
$r = Send-Request -Method POST -Path '/api/target' -Body $big
Assert-That 'Body over 1 MB -> 413'               '413' $r.Status

Write-Host ''
Write-Host 'Failure injection (optional feature)'
$request = [System.Net.Http.HttpRequestMessage]::new(
    [System.Net.Http.HttpMethod]::new('POST'), "$BaseUrl/api/target")
$request.Content = [System.Net.Http.StringContent]::new(
    '{"a":1}', [System.Text.Encoding]::UTF8, 'application/json')
$request.Headers.Add('X-Sim-Status', '503')
$response = $client.SendAsync($request).GetAwaiter().GetResult()

if ([int]$response.StatusCode -eq 503) {
    Write-Host '  INFO  X-Sim-* control headers are enabled; forced 503 works' -ForegroundColor Cyan
}
else {
    Write-Host '  INFO  X-Sim-* control headers are off (the shipped default)' -ForegroundColor DarkGray
    Write-Host '        Enable with: Simulator__EnableControlHeaders=true' -ForegroundColor DarkGray
}

Write-Host ''
if ($script:Failed -eq 0) {
    Write-Host ('All {0} checks passed.' -f $script:Passed) -ForegroundColor Green
    exit 0
}

Write-Host ('{0} passed, {1} FAILED.' -f $script:Passed, $script:Failed) -ForegroundColor Red
exit 1
