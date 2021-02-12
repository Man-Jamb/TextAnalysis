using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace TextAnalysis
{
    //реализация АВЛ дерева
    public class Node
    {
        public string key;          //ключ элемента дерева, в нашем случае найденый триплет
        public int count;           //количество нахождений данного триплета в тексте
        byte height;                //высота дерева с корнем в этом узле
        Node left;                  //ссылка на левый узел
        Node right;                 //ссылка на правый узел
        public byte place;          //позиция в списке десяти самых часто встречающихся триплетов (10 если не входит в этот список)

        //конструктор устанавливает ключ и количество нахождений (при первичной обработке текста количество задаётся равным 1)
        public Node(string k, int c)
        {
            key = k;
            height = 1;
            count = c;
            place = 10;
        }

        //перегрузка оператора сравнения для работы с null
        public static bool operator >(Node n1, Node n2)
        {
            int count1 = n1?.count ?? 0;
            int count2 = n2?.count ?? 0;
            return count1 > count2;
        }

        //перегрузка оператора сравнения для работы с null
        public static bool operator <(Node n1, Node n2)
        {
            int count1 = n1?.count ?? 0;
            int count2 = n2?.count ?? 0;
            return count1 < count2;
        }

        //возвращает высоту узла, возвращает 0 если узла не существует
        static byte Height(Node node)
        {
            return node?.height ?? 0;
        }

        //вычисляет разницу высот правого и левого поддерева
        static int BalanceFacrtor(Node node)
        {
            return (Height(node.right) - Height(node.left));
        }

        //выставляет корректное значение высоты, при условии что значения высот левого и правого поддеревьев коректны
        static void FixHeight(Node node)
        {
            byte hl = Height(node.left);
            byte hr = Height(node.right);
            node.height = (byte)(hl > hr ? hl + 1 : hr + 1);
        }

        //поворот вправо
        static Node RotateRight(Node p)
        {
            Node q = p.left;
            p.left = q.right;
            q.right = p;
            FixHeight(p);
            FixHeight(q);
            return q;
        }

        //поворот влево
        static Node RotateLeft(Node q)
        {
            Node p = q.right;
            q.right = p.left;
            p.left = q;
            FixHeight(q);
            FixHeight(p);
            return p;
        }

        //балансировка узла при перевесе высоты одного из поддеревьев на 2
        static Node Balance(Node node)
        {
            FixHeight(node);
            if (BalanceFacrtor(node) == 2)
            {
                if (BalanceFacrtor(node.right) < 0)
                    node.right = RotateRight(node.right);
                return RotateLeft(node);
            }
            if (BalanceFacrtor(node) == -2)
            {
                if (BalanceFacrtor(node.left) > 0)
                    node.left = RotateLeft(node.left);
                return RotateRight(node);
            }
            return node;
        }

        //добавление нового триплета в дерево, при его нахождении вместо добавления увеличивает количество встреч этого элемента
        public static Node Insert(Node node, string k, int c = 1)
        {
            if (node == null) return new Node(k, c);
            if (string.Compare(k, node.key) == 0)
            {
                node.count += c;
                return node;
            }
            else if (string.Compare(k, node.key) < 0)
                node.left = Insert(node.left, k, c);
            else
                node.right = Insert(node.right, k, c);
            return Balance(node);
        }

        //получение узла по ключу, если узел не найден возвращается null
        public static Node GetByKey(Node node, string k)
        {
            if (node == null) return null;
            if (string.Compare(k, node.key) == 0)
            {
                return node;
            }
            else if (string.Compare(k, node.key) < 0)
                return GetByKey(node.left, k);
            else
                return GetByKey(node.right, k);
        }

        //завис всех узлов в список
        public static void GetAll(Node node, List<Node> list)
        {
            if (node != null)
            {
                if (node.left != null)
                {
                    GetAll(node.left, list);
                }
                if (node.right != null)
                {
                    GetAll(node.right, list);
                }
                list.Add(node);
            }
        }
    }

    //обработка текста из файла по полученному пути заданным количеством потоков
    public class TextAnalis
    {
        Node baseNode;                                          //корень дерево содержащего все найденные триплеты и количества из встреч
        Node[] topTen = new Node[10];                           //10 самый часто встречающихся триплетов
        int threadNumber;                                       //количество потоков обработки текста (может быть уменьшено если текст малого размера)
        int readyThread = 0;                                    //количество потоков завершивших работу
        EventWaitHandle handle = new AutoResetEvent(false);     //дескриптор ожидания завершения работы всех потоков
        Node[] nodes;                                           //корни деревьев каждого потока
        bool stopThreads = false;                               //принудительное завершение потоков

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

            //инициализируем массив деревьев согласно полученному числу потоков
            nodes = new Node[threadNumber];

            //передаём потокам фрагменты текста и запускаем их
            for (int i = 0; i < threadNumber; i++)
            {
                List<object> obj = new List<object>();
                obj.Add(texts[i]);
                obj.Add(i);

                Thread thread = new Thread(Analis);
                thread.Start(obj);
            }

            //запускаем поток проверки нажатия клавиши
            Thread stopThread = new Thread(Stop);
            stopThread.Start();

            //ждём окончания работы всех потоков обработки, или нажатия клавиши
            handle.WaitOne();

            //объеденяем результаты всех потоков анализа текста в одно дерево и находим 10 самых часто встречающихся триплетов
            for (int i = 0; i < threadNumber; i++)
            {
                List<Node> list = new List<Node>();
                Node.GetAll(nodes[i], list);                                //получаем массив всех триплетов найдённых iм потоком
                foreach (Node n in list)
                {
                    baseNode = Node.Insert(baseNode, n.key, n.count);       //добавляем каждый триплет и количество его повторений в общее дерево
                    Node node = Node.GetByKey(baseNode, n.key);             //находим добавленный элемент в дереве
                    if (node != null)                                       
                    {
                        //проверяем не занял ли он более высокую позицию по частоте появлений
                        while ((node.place > 0) && (node > topTen[node.place - 1]))
                        {
                            if (topTen[node.place - 1] != null) topTen[node.place - 1].place++;
                            if (node.place != 10)
                            {
                                topTen[node.place] = topTen[node.place - 1];
                            }
                            topTen[node.place - 1] = node;
                            node.place--;
                        }
                    }
                }
            }

            //вывод 10 самых часто встречающихся триплетов и их колличества
            for (int i = 0; i < topTen.Length; i++)
            {
                Console.Write("{0} - {1}", topTen[i].key, topTen[i].count);
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
            List<object> list = (List<object>)obj;      //получение данных
            string str = (string)list[0];               //фрагмент текста для анализа
            int num = (int)list[1];                     //номер дерева в который ведётся запись найденных триплетов

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
                    Thread.Sleep(0);
                    continue;
                }
                //если второй символ предполагаемого триплета - не буква, то пропустить 2 символа
                if (!(char.IsLetter(str[i + 1])))
                {
                    i++;
                    Thread.Sleep(0);
                    continue;
                }
                //если первый символ предполагаемого триплета - не буква, то пропустить символ
                if (!(char.IsLetter(str[i])))
                {
                    Thread.Sleep(0);
                    continue;
                }

                //если все 3 символа - буквы то добавляем их в дерево
                nodes[num] = Node.Insert(nodes[num], str.Substring(i, 3));


                Thread.Sleep(0);
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
                Console.Write("Укажите путе к текстовому файлу: ");
                path = Console.ReadLine();
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
