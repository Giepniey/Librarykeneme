using MySqlConnector;

/// <summary>
/// Owns the single connection to the LIS database. The connection is created
/// lazily so a failure is reported by <see cref="Database.Initialize"/> with a
/// readable message instead of a TypeInitializationException.
/// </summary>
public class Db
{
    // Defaults match the local XAMPP install; override any of these with
    // environment variables instead of editing source when your setup differs.
    static readonly string server = Env("LIS_DB_SERVER", "127.0.0.1");
    static readonly string port = Env("LIS_DB_PORT", "3306");
    static readonly string db = Env("LIS_DB_NAME", "LIS");
    static readonly string uid = Env("LIS_DB_USER", "root");
    static readonly string pwd = Env("LIS_DB_PASSWORD", "");

    static string Env(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>Host/database/user description used in error messages (never includes the password).</summary>
    public static string Describe => $"{uid}@{server}:{port}/{db}";

    static readonly string con_str = BuildConnectionString();

    static string BuildConnectionString()
    {
        if (!uint.TryParse(port, out var parsedPort))
            parsedPort = 3306;

        return new MySqlConnectionStringBuilder
        {
            Server = server,
            Port = parsedPort,
            Database = db,
            UserID = uid,
            Password = pwd,
            ConnectionTimeout = 5
        }.ConnectionString;
    }

    MySqlConnection connection;

    public Db()
    {
        connection = new MySqlConnection(con_str);

        Exception last = null;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                connection.Open();
                return;
            }
            catch (Exception e)
            {
                last = e;
                Console.WriteLine($"DB connection attempt {i + 1} failed for {Describe}: " + e.Message);
                if (i < 2) System.Threading.Thread.Sleep(1000);
            }
        }

        // Never continue with a connection that was not opened: every later
        // query would fail with a confusing "Connection must be open" error.
        throw new InvalidOperationException(
            $"Could not connect to the MySQL/MariaDB database at {Describe}. " +
            $"Start the database server, make sure the '{db}' database exists " +
            "(run database/00_install.sql), and check the username/password. " +
            "Details: " + (last?.Message ?? "unknown error"), last);
    }

    static Db _instance;
    public static Db Instance => _instance ??= new Db();

    public MySqlConnection Connection => connection;
}
