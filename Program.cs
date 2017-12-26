using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace Counter
{
    class PatternForReplace
    {
        public enum FilterTypes {none, filter, filterLine};
        public Regex[] regex;
        public string[] replaceTo;
        public RegexOptions[] options;
        public FilterTypes[] isFilter;
        public int[] numOfThreads;
    }
    class Executor
    {
        private string inputFileName;
        private string outputFileName;
        private PatternForReplace patternForReplace;
        private Hashtable h = null;
        Regex CounterSearcher;
        public string counter(Match m)
        {
            string s = m.Groups["counter_name"].Value;
            if (h.Contains(s))
                h[s] = ((int)h[s]) + 1;
            else
                h[s] = 1;
            return ((int)h[s]).ToString();
        }
        public Executor(string inputFileName, string outputFileName, PatternForReplace patternForReplace, Regex CounterSearcher)
        {
            this.inputFileName = inputFileName;
            this.outputFileName = outputFileName;
            this.patternForReplace = patternForReplace;
            this.CounterSearcher = CounterSearcher;
        }
        public void execute()
        {
            #region Local variables
            string inputText;
            string resultText;
            #endregion
            #region Reading input file
            if (inputFileName.ToLower() == "console")
            {
                using (StreamReader f1 = new StreamReader(
                            Console.OpenStandardInput(),
                            System.Text.Encoding.Default))
                {
                    inputText = f1.ReadToEnd();
                    f1.Close();
                }
            }
            else
                using (StreamReader f1 = new StreamReader(
                            File.OpenRead(inputFileName),
                            System.Text.Encoding.Default))
                {
                    inputText = f1.ReadToEnd();
                    f1.Close();
                }
            #endregion
            #region Replacing
            resultText = inputText;
            StringBuilder buffer = new StringBuilder();
            for (int i = 0; i < patternForReplace.regex.Length; i++)
            {
                switch (patternForReplace.isFilter[i])
                {
                    case PatternForReplace.FilterTypes.none:
                        resultText = patternForReplace.regex[i].Replace(resultText, patternForReplace.replaceTo[i]);
                        h = new Hashtable();
                        resultText = CounterSearcher.Replace(resultText, counter);
                        break;
                    case PatternForReplace.FilterTypes.filterLine:
                        string[] lines = resultText.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                        foreach (string line in lines)
                            if (patternForReplace.regex[i].IsMatch(line))
                                buffer.AppendLine(line);
                        resultText = buffer.ToString();
                        break;
                    case PatternForReplace.FilterTypes.filter:
                        MatchCollection matches = patternForReplace.regex[i].Matches(resultText);
                        foreach (Match match in matches)
                            buffer.AppendLine(match.ToString());
                        resultText = buffer.ToString();
                        break;
                    default:
                        break;
                }
            }
            #endregion
            #region Writing result file
            if (outputFileName.ToLower() == "console")
            {
                Console.Write(resultText);
            }
            else
                using (StreamWriter f2 = new StreamWriter(
                           File.Create(outputFileName),
                           System.Text.Encoding.Default))
                {
                    f2.Write(resultText);
                    f2.Close();
                }
            #endregion
        }
        public static void start(object executor)
        {
            ((Executor)(executor)).execute();
        }
    }
    class Program
    {
        public void doReplace(string[] args)
        {
            Regex CounterSearcher = new Regex(@"\${counter(?<counter_name>[^}]*)}", RegexOptions.Compiled | RegexOptions.Multiline);
            #region Initializing file names
            string inputFileName = "input.txt";
            if (args.Length > 0)
                inputFileName = args[0];
            string outputFileName = "result.txt";
            if (args.Length > 1)
                outputFileName = args[1];
            string patternFileName = "pattern.xml";
            if (args.Length > 2)
                patternFileName = args[2];
            string listOption = "single";
            if (args.Length > 3)
                listOption = args[3];
            #endregion
            if (outputFileName.ToLower() != "console")
            {
                Console.WriteLine("Input file name: {0}", inputFileName);
                Console.WriteLine("Result file name: {0}", outputFileName);
                Console.WriteLine("Pattern configuration file name: {0}", patternFileName);
            }
            PatternForReplace patternForReplace = readPatternFromXML(patternFileName);
            switch (listOption.ToLower())
            {
                case "single":
                    Executor executor = new Executor(inputFileName, outputFileName, patternForReplace, CounterSearcher);
                    executor.execute();
                    break;
                case "mask":
                    ArrayList threads = new ArrayList();
                    foreach (string fileName in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), inputFileName))
                    {
                        threads.Add(new Thread(Executor.start));
                        ((Thread)(threads[threads.Count - 1])).Start(new Executor(fileName, fileName, patternForReplace, CounterSearcher));
                    }
                    foreach(Thread thread in threads)
                    {
                        thread.Join();
                    }
                    break;
                default:
                    break;
            }
        }
        #region Reading patterns from XML configuration file
        public PatternForReplace readPatternFromXML(string fileName)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.Load(fileName);

            XmlNodeList list = xmlDoc.SelectNodes("patterns/pattern");
            PatternForReplace result = new PatternForReplace();
            result.regex = new Regex[list.Count];
            result.replaceTo = new string[list.Count];
            result.options = new RegexOptions[list.Count];
            result.isFilter = new PatternForReplace.FilterTypes[list.Count];
            result.numOfThreads = new int[list.Count];
            int i = 0;
            for (int j = 0; j < list.Count; ++j)
                result.options[j] = RegexOptions.IgnoreCase;
            foreach (XmlNode e in list)
            {
                switch (e.SelectSingleNode("singleLine").InnerText.ToLower())
                {
                    case "false":
                        result.isFilter[i] = PatternForReplace.FilterTypes.none;
                        result.replaceTo[i] = e.SelectSingleNode("replaceTo").InnerText;
                        break;
                    case "true":
                        result.options[i] |= RegexOptions.Multiline;
                        result.isFilter[i] = PatternForReplace.FilterTypes.none;
                        result.replaceTo[i] = e.SelectSingleNode("replaceTo").InnerText;
                        break;
                    case "filterline":
                        result.isFilter[i] = PatternForReplace.FilterTypes.filterLine;
                        break;
                    case "filter":
                        result.isFilter[i] = PatternForReplace.FilterTypes.filter;
                        break;
                    default:
                        break;
                }
                result.regex[i] = new Regex(e.SelectSingleNode("search").InnerText,
                                        result.options[i]);
                if (e.SelectSingleNode("threads") != null && !String.IsNullOrEmpty(e.SelectSingleNode("threads").InnerText))
                    result.numOfThreads[i] = int.Parse(e.SelectSingleNode("threads").InnerText);
                ++i;
            }
            return result;
        }
        #endregion
        static void Main(string[] args)
        {
            Program program = new Program();
            program.doReplace(args);
        }
    }
}
