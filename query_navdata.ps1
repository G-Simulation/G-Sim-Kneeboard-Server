[System.Reflection.Assembly]::LoadFile('d:\Kneeboard Server\Kneeboard Server\bin\x64\Debug\System.Data.SQLite.dll') | Out-Null
$conn = New-Object System.Data.SQLite.SQLiteConnection('Data Source=d:\Kneeboard Server\Kneeboard Server\bin\x64\Debug\data\msfs_navdata.sqlite')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT ident, num_departures, num_arrivals FROM airport WHERE ident IN ('EDDM', 'EDDF', 'EHAM', 'KJFK', 'EGLL', 'LOWW') ORDER BY ident"
$reader = $cmd.ExecuteReader()
while($reader.Read()) {
    Write-Host $reader['ident'] "SIDs:" $reader['num_departures'] "STARs:" $reader['num_arrivals']
}
$conn.Close()
