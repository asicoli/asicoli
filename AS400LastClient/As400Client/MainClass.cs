using System;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Xml.Serialization;
using System.Text;

namespace As400Client
{
    class MainClass
    {
        internal static string pathLog = ReadSetting("logPath")  + DateTime.Now.ToString("yyyyMMddHHmmss") + "_AS400_" + "log.txt";

        //static string s3Path = ReadSetting("s3Path");

        public static string ReadSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                string result = appSettings[key] ?? "Not Found";
                //Console.WriteLine(result);
                return result;
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
            }
            return "Error reading app settings";
        }

        static void Main(string[] args)
        {

            /*
            //Console.WriteLine(args.Length);
            //Console.WriteLine(args[0]);
            if (args.Length == 1 && args[0].Equals("FULL_ANAF")) {
                Console.WriteLine("Performing FULL ANAF download...");
                SQLtoCSV.tableToCSV("HIPPOCF.ANAF", "", "", 500);
                Environment.Exit(0);
            }
            */

            List<PersistenceObj> readObj = ReadPersistence();

            foreach (PersistenceObj obj in readObj)
            {
                if(obj.applyToCustomQuery!=null && obj.applyToCustomQuery == true)
                {
                    try
                    {
                        //Console.WriteLine("CUSTOM QUERY: " + ReadSetting(obj.tableName));
                        //string[] prams = obj.whereField.Split('|');
                        //List<string> paramL = new List<string>(prams);
                        //Console.WriteLine("SUBSTITUTED QUERY:" + SQLtoCSV.substParamas(ReadSetting(obj.tableName), paramL));
                        string finalQuery = ReadSetting(obj.tableName).Replace(obj.whereField, obj.lastValue);
                        Console.WriteLine("REPLACED CUSTOM QUERY: \n" + finalQuery);
                        SQLtoCSV.customQueryeToCSV(obj.tableName, finalQuery, 500);
                        string newCMax = SQLtoCSV.getMax(ReadSetting(obj.tableName + "_MAX"), "", "");
                        Console.WriteLine("MAX FROM TABLE: " + newCMax);
                        updateLast(readObj, obj.tableName, newCMax);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Error processing custom query for table: " + obj.tableName);
                        Console.WriteLine(e.Message);
                    }

                    continue;
                }

                string selectS = "";

                if (obj.isString)
                {
                    selectS = obj.whereField + ">'" + obj.lastValue + "'";
                }
                else
                {
                    selectS =  obj.whereField + ">" + obj.lastValue;           
                }

                SQLtoCSV.tableToCSV(obj.tableName, obj.whereField, selectS, 500);
                Console.WriteLine("SELECT: " + selectS);
                string newMax = SQLtoCSV.getMax("",obj.tableName, obj.whereField);
                Console.WriteLine("MAX FROM TABLE: " + newMax);
                updateLast(readObj, obj.tableName, newMax);

            }
            
            if(readObj.Count != 0)
            {
                WritePersistence(readObj);
            }


            //Environment.Exit(0);
            //------------------------------------------------------------------------------

            /*
            //string toDayANAF = DateTime.Now.AddDays(-5).ToString("yyyyMMdd") + "0";
            string toDayANAF = DateTime.Now.AddDays(-2).ToString("yyyyMMdd") + "0";
            //string toDayANAF = "202006010";
            */
            string fullTables = ReadSetting("fullTables");

            string[] listF = fullTables.Split(',');

            if (listF!=null && listF.Length != 0)
            {
                foreach (string table in listF)
                {
                    if (!table.Equals(""))
                    {
                        try
                        {
                            //NB: per velocizzare il processo è stato eliminato l'ORDER BY per le tabelle in full...potenzialmente pericoloso! Verificare che sia corretto...
                            Console.WriteLine("Processing Table: " + table);
                            SQLtoCSV.tableToCSV(table, "", "", 500);
                            Console.WriteLine("Completed Tables: " + table);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                }
            }


            Environment.Exit(0);


            //SQLtoCSV.tableToCSV("HIPPOCF.ANAF", "", "AFDULT > " + toDayANAF, 500);

            /*
            SQLtoCSV.tableToCSV("HIPPOCF.ANAP", "APDPRO", "",500);
            SQLtoCSV.tableToCSV("HIPPOCF.ANFAT", "FACODI", "", 500);
            SQLtoCSV.tableToCSV("HIPPOCF.ANFCP", "FPRSOC", "", 500);
            SQLtoCSV.tableToCSV("HIPPOCF.ANFGT", "FTDESC", "", 500);
            SQLtoCSV.tableToCSV("HIPPOCF.ANFPA", "FPDESC", "", 500);
            SQLtoCSV.tableToCSV("HIPPOCF.ANAE", "AECFOR", "", 500);
            SQLtoCSV.tableToCSV("HIPPOCF.ANAG", "AGRSO2", "", 500);
            SQLtoCSV.tableToCSV("HIPPOCF.LISTI", "LICPRO", "", 500);
            */


            //Console.WriteLine("\npress any key to exit the process...");
            //Console.ReadKey();

        }

        public static void updateLast(List<PersistenceObj> persistentTables, string tableName, string lastValue)
        {
            foreach (PersistenceObj obj in persistentTables)
            {
                if (obj.tableName.Equals(tableName))
                {
                    obj.lastValue = lastValue;
                }
            }
        }

        public static void WritePersistence(List<PersistenceObj> persistentTables)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<PersistenceObj>));

            StringBuilder output = new StringBuilder();
            var writer = new StringWriter(output);

            serializer.Serialize(writer, persistentTables);

            //Console.WriteLine(output.ToString());
            try
            {
                System.IO.File.Copy(ReadSetting("persistencePath") + "\\tableMemory.xml", ReadSetting("persistencePath") + "\\tableMemory.bkp", true);
                //System.IO.File.Delete(ReadSetting("persistencePath") + "\\tableMemory.xml");
                File.WriteAllText(ReadSetting("persistencePath") + "\\tableMemory.xml", output.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            
        }

        public static List<PersistenceObj> ReadPersistence()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<PersistenceObj>));
            try
            {

                // Create a TextReader to read the file.
                FileStream fs = new FileStream(ReadSetting("logPath") + "\\tableMemory.xml", FileMode.Open);
                TextReader reader = new StreamReader(fs);

                // Use the Deserialize method to restore the object's state.
                List<PersistenceObj> readObj = (List<PersistenceObj>)serializer.Deserialize(reader);

                fs.Close();

                return readObj;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return new List<PersistenceObj>();
        }

    }
}
