using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Modelo;

namespace Pruebas
{
    /// <summary>
    /// Pruebas del Modelo: concurrencia, mapa, validaciones, ataques y victoria.
    /// Ejecutar: Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class PruebasModelo
    {
        private static Dictionary<TipoRecurso, int> Costo(TipoRecurso t, int n) => new Dictionary<TipoRecurso, int> { { t, n } };

        // ------------------------------------------------------------ concurrencia

        [Test]
        public void Recursos_ConsumosParalelos_NuncaQuedanNegativos()
        {
            var r = new Recursos(1000, 0, 0);
            int exitos = 0;
            Parallel.For(0, 1500, _ => { if (r.IntentarConsumir(Costo(TipoRecurso.Oro, 1))) Interlocked.Increment(ref exitos); });
            Assert.AreEqual(1000, exitos);
            Assert.AreEqual(0, r.Obtener(TipoRecurso.Oro));
        }

        [Test]
        public void Recursos_CostoCompuesto_EsAtomico()
        {
            var r = new Recursos(oroInicial: 10, maderaInicial: 100, comidaInicial: 0);
            var costo = new Dictionary<TipoRecurso, int> { { TipoRecurso.Madera, 50 }, { TipoRecurso.Oro, 20 } };
            Assert.IsFalse(r.IntentarConsumir(costo));
            Assert.AreEqual(100, r.Obtener(TipoRecurso.Madera), "Si falta oro no se debe descontar la madera");
        }

        [Test]
        public void Mapa_ExtraccionesParalelas_NoSacanDeMas()
        {
            var mapa = new Mapa();
            var pos = new Posicion(3, 3);
            mapa.ColocarRecursoNatural(pos, TipoRecurso.Madera, 150);
            int total = 0;
            Parallel.For(0, 80, i => Interlocked.Add(ref total, mapa.ExtraerRecurso(pos, 8, out var _t)));
            Assert.AreEqual(150, total);
            Assert.IsTrue(mapa.EstaLibre(pos), "Un recurso agotado libera la casilla");
        }

        [Test]
        public void Unidad_DañoParalelo_SoloUnGolpeLaMata()
        {
            var u = new Unidad("u", TipoUnidad.Infante, new Posicion(0, 0), "j");
            int muertes = 0;
            Parallel.For(0, 200, _ => { if (u.RecibirDaño(5)) Interlocked.Increment(ref muertes); });
            Assert.AreEqual(1, muertes);
            Assert.AreEqual(0, u.VidaActual);
        }

        [Test]
        public void Unidad_RecargaEsExclusiva()
        {
            var u = new Unidad("u", TipoUnidad.Arquero, new Posicion(0, 0), "j");
            int iniciadas = 0;
            Parallel.For(0, 50, _ => { if (u.IntentarIniciarRecarga()) Interlocked.Increment(ref iniciadas); });
            Assert.AreEqual(1, iniciadas);
        }

        // ------------------------------------------------------------ mapa y validaciones

        [Test]
        public void Mapa_NoPermiteSobreponerNiSalirse()
        {
            var mapa = new Mapa();
            var ed = new Edificio("e", TipoEdificio.Casa, new Posicion(2, 2), "j");
            Assert.IsTrue(mapa.ColocarEdificio(ed));
            Assert.IsFalse(mapa.ColocarRecursoNatural(new Posicion(2, 2), TipoRecurso.Oro, 10));
            Assert.IsFalse(mapa.ColocarRecursoNatural(new Posicion(-1, 0), TipoRecurso.Oro, 10));
            Assert.IsFalse(mapa.ColocarRecursoNatural(new Posicion(0, 15), TipoRecurso.Oro, 10));
        }

        [Test]
        public void Mapa_RutaEsquivaObstaculos()
        {
            var mapa = new Mapa();
            for (int f = 0; f < 15; f++) if (f != 7) mapa.ColocarRecursoNatural(new Posicion(f, 7), TipoRecurso.Oro, 10);
            var ruta = mapa.BuscarRuta(new Posicion(0, 0), new Posicion(0, 14));
            Assert.IsNotNull(ruta);
            CollectionAssert.Contains(ruta, new Posicion(7, 7));
            mapa.ColocarRecursoNatural(new Posicion(7, 7), TipoRecurso.Oro, 10);
            Assert.IsNull(mapa.BuscarRuta(new Posicion(0, 0), new Posicion(0, 14)));
        }

        [Test]
        public void Inicializacion_CreaCentroYAldeanos()
        {
            var p = InicializadorPartida.CrearPartida("T", Dificultad.Normal, false, semilla: 3);
            foreach (var j in new[] { p.JugadorHumano, p.JugadorIA })
            {
                Assert.IsTrue(j.TieneCentroUrbanoVivo());
                Assert.AreEqual(ReglasJuego.AldeanosIniciales, j.ContarUnidades(TipoUnidad.Aldeano));
            }
        }

        [Test]
        public void UbicacionManual_ValidaCasilla()
        {
            var p = InicializadorPartida.CrearPartida("T", Dificultad.Normal, true, semilla: 3);
            Assert.IsFalse(p.JugadorHumano.TieneCentroUrbanoVivo());
            Assert.IsFalse(InicializadorPartida.UbicarCentroUrbanoManual(p, new Posicion(20, 20)).Exito);
            var libre = p.JugadorHumano.Mapa.CasillasLibresCercanas(new Posicion(7, 7), 1).First();
            Assert.IsTrue(InicializadorPartida.UbicarCentroUrbanoManual(p, libre).Exito);
            Assert.IsTrue(p.Comenzar().Exito);
        }

        [Test]
        public void Acciones_InvalidasSonRechazadas()
        {
            var p = InicializadorPartida.CrearPartida("T", Dificultad.Normal, false, semilla: 5);
            var g = new GestorConcurrencia(p);
            Assert.IsFalse(g.Entrenar(p.JugadorHumano, TipoUnidad.Aldeano).Exito, "Antes de comenzar no se puede actuar");
            p.Comenzar();
            var h = p.JugadorHumano;
            Assert.IsFalse(g.Entrenar(h, TipoUnidad.Infante).Exito, "Sin cuartel no hay infantes");
            Assert.IsFalse(g.Construir(h, TipoEdificio.Casa, h.CentroUrbano.Posicion).Exito, "Casilla ocupada");
            Assert.IsFalse(g.Construir(h, TipoEdificio.Casa, new Posicion(99, 99)).Exito, "Fuera del mapa");
            Assert.IsFalse(g.Atacar(h, h.Unidades.First(), new Posicion(0, 0)).Exito, "Los aldeanos no atacan");
            g.DetenerTodo();
        }

        // ------------------------------------------------------------ ataque y victoria

        private static (Partida, GestorConcurrencia, Unidad) PartidaConSoldado()
        {
            var p = InicializadorPartida.CrearPartida("T", Dificultad.Normal, false, semilla: 9);
            p.Comenzar();
            var h = p.JugadorHumano;
            var pos = h.Mapa.BuscarCasillaLibreAdyacente(h.CentroUrbano.Posicion, 5).Value;
            var soldado = new Unidad("soldado", TipoUnidad.Infante, pos, h.Id);
            h.Mapa.ColocarUnidad(soldado, pos);
            h.AgregarUnidad(soldado);
            return (p, new GestorConcurrencia(p), soldado);
        }

        [Test]
        public void Ataque_ImpactoYFalloSeMarcanEnElMapa()
        {
            var (p, g, soldado) = PartidaConSoldado();
            var ia = p.JugadorIA;
            var centroIA = ia.CentroUrbano.Posicion;

            var r = g.Atacar(p.JugadorHumano, soldado, centroIA);
            Assert.IsTrue(r.Exito);
            StringAssert.StartsWith("Impacto", r.Mensaje);
            Assert.AreEqual(ia.CentroUrbano.VidaMaxima - soldado.DañoEdificios, ia.CentroUrbano.VidaActual);
            Assert.AreEqual(MarcaCasilla.Impacto, ia.Mapa.ObtenerInfo(centroIA).Marca);
            Assert.IsTrue(p.JugadorHumano.TieneRevelada(centroIA));

            Assert.IsFalse(g.Atacar(p.JugadorHumano, soldado, centroIA).Exito, "No puede disparar mientras recarga");
            g.DetenerTodo();
        }

        [Test]
        public void Victoria_AlDestruirCentroUrbanoSinMilitares()
        {
            var (p, g, soldado) = PartidaConSoldado();
            var ia = p.JugadorIA;
            var centro = ia.CentroUrbano;
            int golpes = 0;
            while (p.EnCurso && golpes < 100)
            {
                soldado.TerminarRecarga();  // la prueba no espera el tiempo real de recarga
                g.Atacar(p.JugadorHumano, soldado, centro.Posicion);
                golpes++;
            }
            Assert.AreEqual(EstadoPartida.Finalizada, p.Estado);
            Assert.AreEqual(p.JugadorHumano.Id, p.GanadorId);
            Assert.IsTrue(ia.Mapa.EstaLibre(centro.Posicion), "El edificio destruido desaparece del mapa");
            Assert.AreEqual(Mathf_Ceil(centro.VidaMaxima, soldado.DañoEdificios), golpes);
            g.DetenerTodo();
        }

        [Test]
        public void SinVictoria_MientrasQuedenMilitares()
        {
            var (p, g, soldado) = PartidaConSoldado();
            var ia = p.JugadorIA;
            var pos = ia.Mapa.BuscarCasillaLibreAdyacente(ia.CentroUrbano.Posicion, 5).Value;
            var defensor = new Unidad("defensor", TipoUnidad.Arquero, pos, ia.Id);
            ia.Mapa.ColocarUnidad(defensor, pos);
            ia.AgregarUnidad(defensor);

            var centro = ia.CentroUrbano;
            for (int i = 0; i < 100 && centro.EstaVivo(); i++)
            {
                soldado.TerminarRecarga();
                g.Atacar(p.JugadorHumano, soldado, centro.Posicion);
            }
            Assert.IsFalse(centro.EstaVivo());
            Assert.IsTrue(p.EnCurso, "Con un arquero vivo la IA sigue en juego");
            g.DetenerTodo();
        }

        [Test]
        public void Construccion_TerminaEnSegundoPlano()
        {
            var p = InicializadorPartida.CrearPartida("T", Dificultad.Normal, false, semilla: 4);
            p.Comenzar();
            var g = new GestorConcurrencia(p);
            var h = p.JugadorHumano;
            var lugar = h.Mapa.CasillasLibresCercanas(h.CentroUrbano.Posicion, 20, 5, false).Last();
            Assert.IsTrue(g.Construir(h, TipoEdificio.Casa, lugar).Exito);
            var casa = h.Edificios.First(e => e.Tipo == TipoEdificio.Casa);
            Assert.AreEqual(EstadoConstruccion.EnConstruccion, casa.Estado, "Construir no bloquea: vuelve de inmediato");
            Assert.IsTrue(SpinWait.SpinUntil(() => casa.Estado == EstadoConstruccion.Completado, ReglasJuego.TiempoConstruccionMs(TipoEdificio.Casa) + 3000));
            Assert.AreEqual(10, h.PoblacionMaxima);
            g.DetenerTodo();
            Assert.AreEqual(0, g.TareasActivas);
        }

        private static int Mathf_Ceil(int a, int b) => (a + b - 1) / b;
    }
}
