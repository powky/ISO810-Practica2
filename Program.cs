using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class EmployeeDB
{
    [BsonElement("_id")]
    public ObjectId Id { get; set; }

    [BsonElement("employee")]
    public bool employee { get; set; }

    [BsonElement("name")]
    public string Name { get; set; }

    [BsonElement("doc_num")]
    public string DocNum { get; set; }

    [BsonElement("doc_type")]
    public string DocType { get; set; }

    [BsonElement("1qbill")]
    public decimal FirstQuarterBill { get; set; }

    [BsonElement("2qbill")]
    public decimal SecondQuarterBill { get; set; }

    [BsonElement("full_payment")]
    public decimal FullPayment { get; set; }

    [BsonElement("acc_num")]
    public string AccNum { get; set; }

    [BsonElement("job")]
    public string Job { get; set; }
}

public class Institution
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("rnc")]
    public string Rnc { get; set; }

    [BsonElement("1qday")]
    public string FirstQuarterDay { get; set; }

    [BsonElement("2qday")]
    public string SecondQuarterDay { get; set; }

    [BsonElement("apap_acc")]
    public string ApapAcc { get; set; }

    [BsonElement("college")]
    public string College { get; set; }
}

public class InputPayment
{
    public ObjectId Id { get; set; }
    public string DocNum { get; set; }
    public string DocType { get; set; }
    public decimal Bill { get; set; }
    public DateTime Date { get; set; }
}

public class Program
{
    private MongoClient _client;
    private IMongoDatabase _database;
    private IMongoCollection<EmployeeDB> _emplsCollection;
    private IMongoCollection<Institution> _instCollection;
    private IMongoCollection<InputPayment> _inputPaymentsCollection;

    public Program()
    {
        _client = new MongoClient("mongodb://localhost:27017");
        _database = _client.GetDatabase("unapec");
        _emplsCollection = _database.GetCollection<EmployeeDB>("employees");
        _instCollection = _database.GetCollection<Institution>("institution");
        _inputPaymentsCollection = _database.GetCollection<InputPayment>("input_payments");
    }

    public async Task GenerateReport()
    {

        var unapecEmpls = await _emplsCollection.Find(e => e.employee == true).ToListAsync();
        var instData = await _instCollection.Find(i => i.College == "Ferreteria Americana").FirstOrDefaultAsync();

        if (unapecEmpls.Count == 0)
        {
            Console.WriteLine("No hay empleados para pagar nómina.");
            return;
        }

        string filename = "tss.txt";
        using (StreamWriter file = new StreamWriter(filename))
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            string rnc = instData.Rnc.PadRight(10);
            string college = instData.College.PadRight(30);
            string quarterDay = instData.FirstQuarterDay;
            string quarterDate = DateTime.Now.Day.ToString(quarterDay).PadLeft(8, '0');
            string apapAcc = instData.ApapAcc.PadRight(12);
            decimal totalAmount = 0;

            string totalAmountStr = totalAmount.ToString("0.00").PadLeft(14);

            string header = $"E{college}{rnc}{today}";
            await file.WriteLineAsync(header);

            foreach (var employee in unapecEmpls)
            {
                string docNum = employee.DocNum.PadRight(11);
                string docType = employee.DocType;
                string fullPay = employee.FullPayment.ToString("0.00").PadLeft(12);
                string name = employee.Name.PadRight(50);
                string job = employee.Job.PadRight(30);

                string row = $"D{docNum}{docType}{name}{job}{fullPay}";
                await file.WriteLineAsync(row);
            }

            string count = unapecEmpls.Count.ToString().PadLeft(9, '0');
            string footer = $"S{count}";
            await file.WriteLineAsync(footer);
        }

        Console.WriteLine("Archivo generado de forma satisfactoria.");
    }

    public async Task ProcessInputFile()
    {
        Console.WriteLine("Introduce la ruta del archivo: ");
        string filepath = Console.ReadLine();

        string headerPattern = @"^E.{20}.{10}\d{8}";
        string bodyPattern = @"^D.{11}.{1}.{50}.{30}.{12}";
        string footerPattern = @"^S\d{9}";

        try
        {
            using (StreamReader file = new StreamReader(filepath))
            {
                string line;
                DateTime date = DateTime.Now;

                while ((line = await file.ReadLineAsync()) != null)
                {
                    if (Regex.IsMatch(line, headerPattern))
                    {
                        string dateStr = line.Substring(41, 8); // get the date string
                        date = DateTime.ParseExact(dateStr, "yyyyMMdd", null); // parse the date
                    }
                    else if (Regex.IsMatch(line, bodyPattern))
                    {
                        string docNum = line.Substring(1, 11).Trim();
                        string docType = line.Substring(12, 1);
                        string name = line.Substring(13, 50).Trim();
                        string job = line.Substring(63, 30).Trim();
                        decimal fullPayment = decimal.Parse(line.Substring(93, 12));

                        InputPayment inputPayment = new InputPayment()
                        {
                            DocNum = docNum,
                            DocType = docType,
                            Bill = fullPayment,
                            Date = DateTime.Now
                        };

                        await _inputPaymentsCollection.InsertOneAsync(inputPayment);
                    }
                    else if (Regex.IsMatch(line, footerPattern))
                    {
                        Console.WriteLine("Archivo procesado correctamente.");
                    }
                    else
                    {
                        throw new Exception("Layout no válido, por favor revisa el archivo.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error procesando el archivo: {ex.Message}");
        }
    }

    public async Task ShowMenu()
    {
        while (true)
        {
            Console.WriteLine("\nEscoge una opción:");
            Console.WriteLine("1. Exportar archivo de nómina.");
            Console.WriteLine("2. Importar archivo de nómina.");
            Console.WriteLine("3. Salir");

            string userChoice = Console.ReadLine();

            switch (userChoice)
            {
                case "1":
                    await GenerateReport();
                    break;
                case "2":
                    await ProcessInputFile();
                    break;
                case "3":
                    Console.WriteLine("Saliendo del programa.");
                    return;
                default:
                    Console.WriteLine("Opción inválida, por favor digita una opción del menú.");
                    break;
            }
        }
    }

    public static async Task Main(string[] args)
    {
        Program program = new Program();
        await program.ShowMenu();
    }
}