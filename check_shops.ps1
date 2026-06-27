$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = "Server=103.48.194.169,52840;Database=HCKIMNGUU_TEST_APP;User Id=sse_thuantran;Password=123!@#a;TrustServerCertificate=True"
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT id_shop, ten_shop, key_api FROM dmkb_api WHERE status = '1'"
$reader = $cmd.ExecuteReader()
while($reader.Read()) {
    $hasKey = $reader["key_api"].Length -gt 0
    Write-Host "Shop ID: $($reader['id_shop']) - Name: $($reader['ten_shop']) - HasKey: $hasKey"
}
$conn.Close()
