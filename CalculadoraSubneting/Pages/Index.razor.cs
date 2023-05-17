using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;
using CalculadoraSubneting;
using CalculadoraSubneting.Shared;
using System.Text.RegularExpressions;

namespace CalculadoraSubneting.Pages
{
    public partial class Index
    {
        private List<Subred> listaSubredes = new List<Subred>();
        public string ip;
        public int subredes;
        public bool posible = true;
        public bool tamanoPosible = true;
        public bool VLSM;
        public bool calculado;
        const int MASCARA_MAX = 32;
        const int TAMANO_BYTE = 8;
        const int DECIMAL_BYTE = 255;
        //variables para la tabla de resultados
        public string[] nombresResult;
        public string[] redesResult;
        public string[] rangosResult;
        public string[] broadcastResult;
        public string hostsResult;
        public string mascaraResult;
        public string[] mascaraResultVLSM;
        public List<string> hostsResultVLSM;
        public List<string> nombresResultVLSM;
        private class Subred
        {
            public string Nombre { get; set; }

            public int Tamano { get; set; }
        }

        public bool Comprobar()
        {
            string tresCifras = @"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)";
            string dosCifras = @"(?:[1-9]|[12]\d|3[012])$";
            string filtroRed = $@"\b{tresCifras}\.{tresCifras}\.{tresCifras}\.{tresCifras}\/{dosCifras}";
            Regex red = new Regex(filtroRed); //filtro red con su mascara
            return red.IsMatch(ip);
        }

        public void Calcular()
        {
            if (Comprobar())
            {
                posible = true;
                //Esto comprueba nº de hosts y bits necesarios segun las subredes
                int subRedesNecesarias = 1;
                int bitsNecesarios = 0;
                while (subRedesNecesarias < subredes)
                {
                    subRedesNecesarias *= 2;
                    bitsNecesarios++;
                }

                //int tamanoMaximo = (DECIMAL_BYTE + 1) / subRedesNecesarias;
                //tamanoPosible = listaSubredes.Max(x => x.Tamano) <= tamanoMaximo - 2; //el bool de si es posible o no guarda v si el tamaño k el pasa es <= que el necesario
                //Aqui separamos la mascara de la ip normal
                string[] a = ip.Split('/');
                int mascara = int.Parse(a[1]);
                string ipSinMascara = a[0];
                mascara += bitsNecesarios;
                string[] octetos = ipSinMascara.Split('.');
                string[] octetosBinarios = new string[4];
                for (int i = 0; i < octetos.Length; i++)
                {
                    int octetoDecimal = int.Parse(octetos[i]);
                    octetosBinarios[i] = PasarABinario(octetoDecimal).PadLeft(TAMANO_BYTE, '0'); //esto rellena de 0 a la izquierda
                }

                string octetosBinariosJuntos = string.Empty;
                foreach (string octeto in octetosBinarios) //junta cada octeto a un solo string
                {
                    octetosBinariosJuntos += octeto;
                }

                int numeroBitsHosts = MASCARA_MAX - mascara; //coge los bits que tenemos para hosts
                int totalHosts = int.Parse(Math.Pow(2, numeroBitsHosts + bitsNecesarios).ToString()); //PasarADecimal(bitsParaHosts)+1 ;// comprobar si hay que sumar uno ya que no es lo mismo 2^8 que 8bits lleno de 1 a decimal
                int hostsCadaSubred = totalHosts / subRedesNecesarias; //host por cada subres
                tamanoPosible = listaSubredes.Max(x => x.Tamano) <= hostsCadaSubred - 2 && hostsCadaSubred - 2 >= 1; //el bool de si es posible o no guarda v si el tamaño k el pasa es <= que el necesario
                string[] ipesBinario = new string[subRedesNecesarias + 1];
                for (int i = 0; i < ipesBinario.Length; i++)
                {
                    //Esto mete en ipes todos los numeros juntos(mascara y numero de hosts)
                    ipesBinario[i] = octetosBinariosJuntos.Substring(0, mascara - bitsNecesarios) + PasarABinario(hostsCadaSubred * i - 1).PadLeft(numeroBitsHosts + bitsNecesarios, '0');
                }

                string[] ipesDecimal = new string[subRedesNecesarias + 1]; //se guardaran las  ips en decimal
                for (int i = 0; i < ipesDecimal.Length; i++)
                {
                    for (int j = 0; j < MASCARA_MAX; j += TAMANO_BYTE)
                    {
                        ipesDecimal[i] += PasarADecimal(ipesBinario[i].Substring(j, TAMANO_BYTE)).ToString() + '.'; //te pasa a decimal cada 8 te pone un .
                    }

                    ipesDecimal[i] = ipesDecimal[i].Substring(0, ipesDecimal[i].Length - 1); //te quita el punto del final
                }

                GuardarDatos(mascara, ipesDecimal, hostsCadaSubred, subRedesNecesarias);
            }
            else
            {
                posible = false; // si no cumple las condiciones(para mostrar mensaje de fallo)
            }
        }

        private void Actualiza()
        {
            if (subredes >= listaSubredes.Count) //actualiza si le das un tamaño mas grande del que ya hay(de inputs)
            {
                for (int i = listaSubredes.Count; i < subredes; i++)
                {
                    listaSubredes.Add(new Subred{Nombre = ""});
                }
            }
            else
            {
                for (int i = subredes - 1; i < listaSubredes.Count; i++) //actualiza si le das un tamaño mas pequeño del que ya hay(de inputs)
                {
                    listaSubredes.Remove(listaSubredes[i]);
                }
            }
        }

        public void CalcularVLSM()
        {
            if (Comprobar())
            {
                posible = true;
                List<string> ipSeparada = new List<string>();
                List<string> ipBinariaSeparada = new List<string>();
                List<string> ipsResult = new List<string>();
                string[] a = ip.Split('/');
                int mascaraInicial = int.Parse(a[1]);
                string ipSinMascara = a[0];
                int hostTotales = 0;
                foreach (var item in listaSubredes)
                {
                    hostTotales += item.Tamano + 2;
                }

                tamanoPosible = hostTotales < Math.Pow(2, MASCARA_MAX - mascaraInicial); //suma todos los hosts y ve si caben en la mascara que tenemos
                foreach (var item in ipSinMascara.Split('.'))
                {
                    ipSeparada.Add(item);
                }

                //hasta aqui hace los splits y demas
                foreach (var item in ipSeparada) //convierte cada octeto a binario
                {
                    ipBinariaSeparada.Add(PasarABinario(int.Parse(item)).PadLeft(8, '0'));
                }

                string ipBinaria = string.Empty;
                foreach (string item in ipBinariaSeparada) //junta los octetos binarios
                {
                    ipBinaria += item;
                }

                int bitsParaHostsYSubred = MASCARA_MAX - mascaraInicial; //calcula los bits restantes de la mascara inicial
                string primerElementoLibre = string.Empty; //donde guardamos el primer elemento de la lista de Libres
                for (int i = 0; i < bitsParaHostsYSubred; i++) //rellena el restante desde la mascara hasta el final con 0(primer elemento libre)
                {
                    primerElementoLibre += "0";
                }

                List<string> ipsLibres = new List<string>();
                ipsLibres.Add(primerElementoLibre); //añadimos primer elemento libre
                string substring1 = ipBinaria.Substring(0, ipBinaria.Length - bitsParaHostsYSubred); //para cambiar lo que no es la mascara por 0s
                string primeraDeRed = substring1 + primerElementoLibre;
                ipsResult.Add(primeraDeRed);
                List<int> hosts = new List<int>();
                foreach (var item in listaSubredes)
                {
                    hosts.Add(item.Tamano + 2);
                }

                int cantidadHosts = hosts.Count;
                for (int k = 0; k < cantidadHosts; k++)
                {
                    int hostMayor = hosts.Max(x => x); //cogemos el siguiente host mas grande y lo borramos
                    hosts.Remove(hosts.Max(x => x));
                    int bitsHostMayor = CalcularPotencia(hostMayor); //calcula los bits necesarios para el host
                    ipsLibres.OrderBy(x => PasarADecimal(x)); //ordenamos la lista de ips libres para coger el mas pequño en cada interaccion
                    string ipLibreMenor = string.Empty;
                    bool cogido = false;
                    for (int i = 0; i < ipsLibres.Count && !cogido; i++) //bucle para coger la iplibre con menos tamaño pero que quepa y borrarlo de la lista
                    {
                        int contadorCeros = 0;
                        for (int j = ipsLibres[i].Length - 1; j >= 0; j--) //contador de ceros a la derecha para ver si caben los bits de host en la ip libre
                        {
                            if (ipsLibres[i][j] == '0')
                            {
                                contadorCeros++;
                            }
                        }

                        if (contadorCeros >= bitsHostMayor)
                        {
                            ipLibreMenor = ipsLibres[i];
                            ipsLibres.Remove(ipsLibres[i]);
                            cogido = true;
                        }
                    }

                    string binarioDeHosts = string.Empty;
                    for (int i = 0; i < bitsHostMayor; i++) //depende del tamaño que necesite(bits) lo rellena de 1s
                    {
                        binarioDeHosts += "1";
                    }

                    string substring2 = ipBinaria.Substring(0, ipBinaria.Length - bitsParaHostsYSubred);
                    string substring3 = (substring2 + ipLibreMenor).Substring(0, ipBinaria.Length - bitsHostMayor);
                    string ipBroadcast = substring3 + binarioDeHosts;
                    ipsResult.Add(ipBroadcast); //guardar en una lista resultado, masacaraBinario + iplibremenos + binariohost
                    string substring4 = ipLibreMenor.Substring(0, ipLibreMenor.Length - bitsHostMayor);
                    string paraSiguienteLibre = substring4 + binarioDeHosts;
                    string siguienteLibre = PasarABinario(PasarADecimal(paraSiguienteLibre) + 1).PadLeft(bitsParaHostsYSubred, '0'); //esto te da el siguiente libre
                    ipsLibres.Add(siguienteLibre); //añade a la lista de ips libres
                    int posicionUltimoUno = siguienteLibre.LastIndexOf("1"); //te calcula si en esta interaccion ha dejado mas ips libres
                    string sinUltimoUno = string.Empty;
                    for (int i = 0; i < siguienteLibre.Length; i++)
                    {
                        if (i == posicionUltimoUno + 1)
                        {
                            sinUltimoUno += "0";
                        }

                        sinUltimoUno += siguienteLibre[i];
                    }

                    int posicionPenultimoUno = siguienteLibre.LastIndexOf("1");
                    int vecesRepetirBucle = posicionUltimoUno - posicionPenultimoUno;
                    for (int j = 0; j < vecesRepetirBucle; j++) //si ha dejado mas ips libres aqui las calculamos y las añadimos a la lista de ipslibres
                    {
                        string calculoDisponible = string.Empty;
                        int primerUno = siguienteLibre.LastIndexOf("1");
                        for (int i = 0; i < siguienteLibre.Length; i++)
                        {
                            if (siguienteLibre[i] == '1')
                            {
                                if (i == primerUno)
                                {
                                    calculoDisponible += 0;
                                }
                                else
                                {
                                    calculoDisponible += "1";
                                }
                            }
                            else
                            {
                                if (i == primerUno - 1)
                                {
                                    if (siguienteLibre[i] != '1')
                                    {
                                        calculoDisponible += 1;
                                    }
                                }
                                else
                                {
                                    calculoDisponible += '0';
                                }
                            }
                        }

                        ipsLibres.Add(calculoDisponible);
                    }
                }

                string[] ipesDecimal = new string[listaSubredes.Count + 1];
                for (int i = 0; i < ipesDecimal.Length; i++)
                {
                    for (int j = 0; j < MASCARA_MAX; j += TAMANO_BYTE)
                    {
                        ipesDecimal[i] += PasarADecimal(ipsResult[i].Substring(j, TAMANO_BYTE)).ToString() + '.'; //te pasa a decimal cada 8 te pone un .
                    }

                    ipesDecimal[i] = ipesDecimal[i].Substring(0, ipesDecimal[i].Length - 1); //te quita el punto del final
                }

                //listasubred ordenar
                GuardarDatosVLSM(ipesDecimal);
            }
            else
            {
                posible = false;
            }
        }

        public int CalcularPotencia(int subredes) //true
        {
            int subRedesNecesarias = 1;
            int potencia = 0;
            while (subRedesNecesarias < subredes)
            {
                subRedesNecesarias *= 2;
                potencia++;
            }

            return potencia;
        }

        public void GuardarDatosVLSM(string[] ipesDecimal)
        {
            listaSubredes.OrderBy(x => x);
            string[] redes = new string[subredes]; //creamos arrays con un tamaño
            string[] broadcast = new string[subredes];
            string[] rango = new string[subredes];
            string[] nombres = new string[subredes];
            string[] mascara = new string[subredes];
            string[] hosts = new string[subredes];
            nombresResultVLSM = new List<string>();
            hostsResultVLSM = new List<string>();
            Dictionary<string, string> nombreYtamano = new Dictionary<string, string>();
            for (int i = 0; i < subredes; i++)
            {
                if (i == 0)
                {
                    redes[i] = ipesDecimal[i]; //el primero ya que es la 00
                }
                else
                {
                    redes[i] = Suma1(ipesDecimal[i]);
                }

                rango[i] = Suma1(redes[i]) + " - " + Resta1(ipesDecimal[i + 1]);
                broadcast[i] = ipesDecimal[i + 1];
                nombreYtamano.Add(listaSubredes[i].Nombre, Math.Pow(2, CalcularPotencia(listaSubredes[i].Tamano)).ToString());
                mascara[i] = (MASCARA_MAX - CalcularPotencia(listaSubredes[i].Tamano)).ToString();
            }

            mascaraResultVLSM = mascara.ToList().OrderBy(x => int.Parse(x)).ToArray();
            foreach (var item in nombreYtamano.OrderByDescending(x => int.Parse(x.Value)))
            {
                nombresResultVLSM.Add(item.Key);
                hostsResultVLSM.Add(item.Value);
            }

            redesResult = redes;
            broadcastResult = broadcast;
            rangosResult = rango;
            calculado = true;
        }

        public void GuardarDatos(int mascara, string[] ipesDecimal, int hosts, int subredes)
        {
            mascaraResult = "/" + mascara.ToString();
            hostsResult = (hosts - 2).ToString();
            string[] redes = new string[subredes]; //creamos arrays con un tamaño
            string[] broadcast = new string[subredes];
            string[] rango = new string[subredes];
            string[] nombres = new string[subredes];
            for (int i = 0; i < subredes; i++)
            {
                if (i == 0)
                {
                    redes[i] = ipesDecimal[i]; //el primero ya que es la 00
                }
                else
                {
                    redes[i] = Suma1(ipesDecimal[i]);
                }

                rango[i] = Suma1(redes[i]) + " - " + Resta1(ipesDecimal[i + 1]);
                broadcast[i] = ipesDecimal[i + 1];
                if (i >= listaSubredes.Count)
                {
                    nombres[i] = "Red Libre";
                }
                else
                {
                    nombres[i] = listaSubredes[i].Nombre;
                }
            //nombresResult[i] = listaSubredes[i].Nombre ?? "Red Libre";
            }

            nombresResult = nombres;
            redesResult = redes;
            broadcastResult = broadcast;
            rangosResult = rango;
            calculado = true;
        }

        public string Suma1(string ip) //separa octetos y al ultimo le suma uno
        {
            string[] octetos = ip.Split('.');
            octetos[3] = (int.Parse(octetos[3]) + 1).ToString();
            for (int i = octetos.Length - 1; i > 0; i--)
            {
                if (octetos[i] == "256")
                {
                    octetos[i] = "0";
                    octetos[i - 1] = (int.Parse(octetos[i - 1]) + 1).ToString();
                }
            }

            return string.Join('.', octetos);
        }

        public string Resta1(string ip) //separa octetos y al ultimo le resta uno
        {
            string[] octetos = ip.Split('.');
            octetos[3] = (int.Parse(octetos[3]) - 1).ToString();
            for (int i = octetos.Length - 1; i > 0; i--)
            {
                if (octetos[i] == "-1")
                {
                    octetos[i] = "255";
                    octetos[i - 1] = (int.Parse(octetos[i - 1]) - 1).ToString();
                }
            }

            return string.Join('.', octetos);
        }

        public int PasarADecimal(string numeroBinario)
        {
            int decimalNumber = 0; //numero empieza en 0
            for (int i = 0; i < numeroBinario.Length; i++) //recorre numero binario
            {
                decimalNumber += int.Parse(numeroBinario[i].ToString()) * (int)Math.Pow(2, numeroBinario.Length - i - 1); //cada binario lo multiplica por su valor
            }

            return decimalNumber;
        }

        public string PasarABinario(int numero)
        {
            string numeroBinario = ""; // cadena vacía para almacenar el número binario
            while (numero > 0) // mientras el número decimal sea mayor que cero
            {
                int resto = numero % 2; // calcula el resto de la división por 2
                numeroBinario = resto.ToString() + numeroBinario; // agrega el dígito al número binario
                numero /= 2; // divide el número decimal entre 2
            }

            return numeroBinario;
        }
    }
}