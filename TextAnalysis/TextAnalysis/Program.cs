using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace TextAnalysis
{
    
    //обработка текста из файла по полученному пути заданным количеством потоков
    public class TextAnalis
    {
        ConcurrentDictionary<string, int> dictionary = new ConcurrentDictionary<string, int>();     //потокобезопасный словарь для хранения найденных в тексте триплетов
        int threadNumber;                                                                           //количество потоков обработки текста (может быть уменьшено если текст малого размера)
        int readyThread = 0;                                                                        //количество потоков завершивших работу
        EventWaitHandle handle = new AutoResetEvent(false);                                         //дескриптор ожидания завершения работы всех потоков
        bool stopThreads = false;                                                                   //принудительное завершение потоков
        string[] topTen = new string[10];                                                           //массив содержащий 10 самых часто встречающихся триплетов
        int[] topTenCounts = new int[10];                                                           //массив содержащий количество нахождений десяти самых часто встречающихся триплетов

        public TextAnalis(string path, int numberOfThreads)
        {
            string text = "";
            threadNumber = numberOfThreads;

            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    text = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            if (text.Length == 0)
            {
                Console.WriteLine("Файл пуст");
                return;
            }

            //уменьшение количества потоков из продположения, что нет смысла выделять отдельный поток для текста менее 100 символов
            if (text.Length < threadNumber * 100) threadNumber = text.Length / 100;

            string[] texts = new string[threadNumber];              //масив содержащий поделённый текст
            int startMarker = 0;                                    //маркер начала нового фрагмента текста
            int baseShift = (int)(text.Length / threadNumber);      //минимальный размер участка текста

            //цикл разделяет текст на приблизительно равные части не нарушая целостность слов
            for (int i = 0; i < threadNumber - 1; i++)
            {
                int k = 0;
                //поиск первого символа не являющегося буквой
                while (char.IsLetter(text[startMarker + baseShift + k]))
                {
                    k++;
                    //проверка выхода за пределы текста
                    if (!(startMarker + baseShift + k < text.Length - 1))
                    {
                        break;
                    }
                }
                texts[i] = text.Substring(startMarker, baseShift + k);
                startMarker += baseShift + k + 1;

                //если мы достигли конца текста то уменьшаем количество потоков согласно количкству полученных отрывков текста
                if (!(startMarker < text.Length - 1))
                {
                    threadNumber = i + 1;
                    break;
                }
            }

            //если маркер начала не дошёл до конца (что является нормальной ситуацией) то оставшийся текст передаётся последнему потоку
            if (startMarker < text.Length - 1)
            {
                texts[threadNumber - 1] = text.Substring(startMarker);
            }

            //передаём потокам фрагменты текста и запускаем их
            for (int i = 0; i < threadNumber; i++)
            {
                Thread thread = new Thread(Analis);
                thread.Start(texts[i]);
            }

            //запускаем поток проверки нажатия клавиши
            Thread stopThread = new Thread(Stop);
            stopThread.Start();

            //ждём окончания работы всех потоков обработки, или нажатия клавиши
            handle.WaitOne();

            //находим 10 самых часто встречающихся триплетов в тексте
            foreach (KeyValuePair<string, int> trip in dictionary)
            {
                for (int i = 10; i > 0; i--)
                {
                    if (trip.Value > topTenCounts[i - 1]) 
                    {
                        if (i != 10)
                        {
                            topTen[i] = topTen[i - 1];
                            topTenCounts[i] = topTenCounts[i - 1];
                        }
                        topTen[i - 1] = trip.Key;
                        topTenCounts[i - 1] = trip.Value;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            //вывод 10 самых часто встречающихся триплетов и их колличества
            for (int i = 0; i < topTen.Length; i++)
            {
                Console.Write("{0} - {1}", topTen[i], topTenCounts[i]);
                if (i != topTen.Length - 1) 
                    Console.Write(", ");
            }

            Console.WriteLine();
        }

        //функция проверки нажатия клавиши
        void Stop()
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    stopThreads = true;
                    Thread.Sleep(50);
                    handle.Set();
                    break;
                }
                Thread.Sleep(50);
            }
        }

        //функция анализа полученного фрагмента текста
        void Analis(object obj)
        {
            string str = (string)obj;                       //получение фрагмента текста для анализа

            for (int i = 0; i < str.Length - 2; i++)
            {
                //если была нажата клавиша завыршить цикл
                if (stopThreads)
                {
                    break;
                }

                //если третий символ предполагаемого триплета - не буква, то пропустить 3 символа
                if (!(char.IsLetter(str[i + 2]))) 
                {
                    i += 2;
                    continue;
                }
                //если второй символ предполагаемого триплета - не буква, то пропустить 2 символа
                if (!(char.IsLetter(str[i + 1])))
                {
                    i++;
                    continue;
                }
                //если первый символ предполагаемого триплета - не буква, то пропустить символ
                if (!(char.IsLetter(str[i])))
                {
                    continue;
                }

                //если все 3 символа - буквы то добавляем их в словарь
                dictionary.AddOrUpdate(str.Substring(i, 3), 1, (id, count) => count + 1);
            }


            //увеличиваем счётчик завершённых потоков
            readyThread++;
            //если это последний поток, то сигнализируем, что все потоки завершили работу
            if (readyThread == threadNumber)
            {
                handle.Set();
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string path;
            if ((args.Length > 0) && (args[0] != ""))
            {
                path = args[0];
            }
            else
            {
                path = @"C:\Users\Родион\Desktop\тестовое задание\Crossinform\txt3.txt";
                //Console.Write("Укажите путе к текстовому файлу: ");
                //path = Console.ReadLine();
            }
            
            Stopwatch watch = new Stopwatch();
            watch.Start();

            //передача пути к файлу, и количество потоков анализатору текста
            TextAnalis analis = new TextAnalis(path, 16);

            watch.Stop();
            Console.WriteLine("Время выполнения: " + watch.ElapsedMilliseconds);
        }

        
    }
}
