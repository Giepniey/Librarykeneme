using MySqlConnector;

public class Db
{
    static string server = "127.0.0.1";
    static string port = "3306";
    static string db = "LIS";
    static string uid = "root";

    string? sql;

    static string con_str = $"Server={server};Port={port};Database={db};Uid={uid};Pwd=";
    MySqlConnection connection;

    public Db()
    {
        connection = new MySqlConnection(con_str);
        for (int i = 0; i < 3; i++)
        {
            try
            {
                connection.Open();
                break;
            }
            catch (Exception e)
            {
                Console.WriteLine($"DB connection attempt {i + 1} failed: " + e.Message);
                if (i < 2) System.Threading.Thread.Sleep(1000);
            }
        }
    }

    public static Db Instance { get; } = new Db();

    public MySqlConnection Connection => connection;
}
