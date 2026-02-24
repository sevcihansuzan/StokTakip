using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.Generic;
using SideBar_Nav.Pages;

namespace SideBar_Nav.Services
{
    public class IotDatabaseService
    {
        private readonly string _connectionString =
            @"Data Source=DESKTOP-2Q11CCH\SQLEXPRESS;Initial Catalog=IOTDB;Integrated Security=True";

        // 1) SENSOR LOG INSERT
        public async Task LogSensorDataAsync(
            string makineId,
            DateTime sensorZaman,
            string uptime,
            double voltaj,
            double akim_mA,
            double guc_mW,
            double sicaklik,
            int rpm
        )
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
INSERT INTO SensorLoglari
(MakineID, Zaman, SensorZaman, Uptime, Voltaj, Akim, Guc_mW, Sicaklik, RPM)
VALUES
(@mid, GETDATE(), @sz, @up, @v, @a, @g, @s, @rpm);";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@mid", makineId);
                    cmd.Parameters.AddWithValue("@sz", sensorZaman);
                    cmd.Parameters.AddWithValue("@up", uptime ?? "");
                    cmd.Parameters.AddWithValue("@v", voltaj);
                    cmd.Parameters.AddWithValue("@a", akim_mA);
                    cmd.Parameters.AddWithValue("@g", guc_mW);
                    cmd.Parameters.AddWithValue("@s", sicaklik);
                    cmd.Parameters.AddWithValue("@rpm", rpm);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // KPI MODEL
        public class UretimKpiModel
        {
            public double SureSn { get; set; }
            public double KwhUrun { get; set; }
            public double Verim { get; set; }
        }

        // 2) KPI GETİR (ŞİMDİLİK "Tamamlandı" ÜZERİNDEN)
        public async Task<UretimKpiModel> GetSonUretimKpiAsync()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Notlar:
                // - Güç mW ise: W = mW/1000
                // - kWh = (OrtalamaWatt * SureSaat)/1000
                string query = @"
SELECT
    UA.UrunRFID,
    DATEDIFF(SECOND, UA.BaslangicZamani, UA.BitisZamani) AS SureSn,
    ISNULL(
        (AVG(SL.Guc_mW) / 1000.0)
        * (DATEDIFF(SECOND, UA.BaslangicZamani, UA.BitisZamani) / 3600.0),
        0
    ) AS KwhUrun,
    100.0 AS Verim
FROM UretimAkisi UA
LEFT JOIN SensorLoglari SL
    ON SL.SensorZaman BETWEEN UA.BaslangicZamani AND UA.BitisZamani
WHERE UA.IslemDurumu = 'Tamamlandı'
GROUP BY
    UA.UrunRFID,
    UA.BaslangicZamani,
    UA.BitisZamani
ORDER BY UA.BaslangicZamani DESC;
";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader rd = await cmd.ExecuteReaderAsync())
                {
                    if (await rd.ReadAsync())
                    {
                        double sure = rd["SureSn"] == DBNull.Value ? 0 : Convert.ToDouble(rd["SureSn"]);
                        double kwh = rd["KwhUrun"] == DBNull.Value ? 0 : Convert.ToDouble(rd["KwhUrun"]);
                        double verim = rd["Verim"] == DBNull.Value ? 0 : Convert.ToDouble(rd["Verim"]);

                        return new UretimKpiModel
                        {
                            SureSn = sure,
                            KwhUrun = kwh,
                            Verim = verim

                        };
                    }
                }
            }
            return null;
        }

        // 3) SON 50 SENSÖR KAYDI
        public async Task<List<SensorDataModel>> GetSonSensorKayitlariAsync()
        {
            var liste = new List<SensorDataModel>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
SELECT TOP 50 MakineID, SensorZaman, Voltaj, Akim, Guc_mW, Sicaklik
FROM SensorLoglari
ORDER BY SensorZaman DESC;";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        double v = reader["Voltaj"] == DBNull.Value ? 0 : Convert.ToDouble(reader["Voltaj"]);
                        double a = reader["Akim"] == DBNull.Value ? 0 : Convert.ToDouble(reader["Akim"]);
                        double t = reader["Sicaklik"] == DBNull.Value ? 0 : Convert.ToDouble(reader["Sicaklik"]);

                        DateTime zaman = reader["SensorZaman"] == DBNull.Value
                            ? DateTime.MinValue
                            : (DateTime)reader["SensorZaman"];

                        string makineId = Convert.ToString(reader["MakineID"]) ?? "-";

                        liste.Add(new SensorDataModel
                        {
                            UrunID = "KAYITLI_VERI",
                            SensorNoktasi = makineId,
                            Zaman = zaman,
                            Vrms = $"{v:0.0} V",
                            Irms = $"{a:0.00} A",
                            Guc = $"{(v * a):0.0} W",
                            Sicaklik = $"{t:0.0} °C"
                        });


                    }
                }
            }

            return liste;
        }

        public async Task MotorUretimBaslatAsync(string makineId, DateTime baslangic)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
INSERT INTO UretimAkisi
(UrunRFID, MakineID, BaslangicZamani, IslemDurumu)
VALUES
('MOTOR_OTOMATIK', @mid, @bas, 'Sürüyor');";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@mid", makineId);
            cmd.Parameters.AddWithValue("@bas", baslangic);

            await cmd.ExecuteNonQueryAsync();
        }
        public async Task MotorUretimBitirAsync(
            string makineId,
            DateTime baslangic,
            DateTime bitis)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
UPDATE UretimAkisi
SET
    BitisZamani = @bit,
    IslemDurumu = 'Tamamlandı'
WHERE
    MakineID = @mid
    AND BaslangicZamani = @bas
    AND IslemDurumu = 'Sürüyor';";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@mid", makineId);
            cmd.Parameters.AddWithValue("@bas", baslangic);
            cmd.Parameters.AddWithValue("@bit", bitis);

            await cmd.ExecuteNonQueryAsync();
        }

    }
}
