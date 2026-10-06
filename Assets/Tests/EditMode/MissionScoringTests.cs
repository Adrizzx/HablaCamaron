using NUnit.Framework;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La fórmula de evaluación del GDD (0-100, aprueba con 70) caso por caso.
    /// Es EL contrato pedagógico del juego: si alguien la desbalancea, estas
    /// pruebas lo delatan antes que un jugador frustrado.
    /// </summary>
    public class MissionScoringTests
    {
        private static MissionStats Perfect() => new MissionStats
        {
            ReachedGoal = true,
            TimeUsed = 100f,
            TimeLimit = 240f,
        };

        [Test]
        public void CorridaPerfecta_Da100()
        {
            var r = MissionScoring.Compute(Perfect(), 0);
            Assert.AreEqual(100, r.totalScore);
        }

        [Test]
        public void NoLlegar_ReprobadoSinObjetivoNiTiempo()
        {
            var s = Perfect();
            s.ReachedGoal = false;
            var r = MissionScoring.Compute(s, 0);
            Assert.AreEqual(0, r.scoreObjective);
            Assert.AreEqual(0, r.scoreTime);
            Assert.Less(r.totalScore, 70, "Sin llegar no se puede aprobar");
        }

        [Test]
        public void CadaSemaforoEnRojo_Descuenta5()
        {
            var s = Perfect();
            s.RedLightsRun = 2;
            var r = MissionScoring.Compute(s, 0);
            Assert.AreEqual(10, r.scoreSignals); // 20 − 2×5
        }

        [Test]
        public void ExcederElLimiteDeVelocidad_CuestaLoMismoQueUnRojo()
        {
            // El GDD descuenta señales por semáforo, PARE y LÍMITE por igual.
            var s = Perfect();
            s.RedLightsRun = 1;
            s.SpeedingTickets = 1;
            var r = MissionScoring.Compute(s, 0);
            Assert.AreEqual(10, r.scoreSignals); // 20 − (1+1)×5
        }

        [Test]
        public void MuchasInfracciones_NoDanPuntajeNegativo()
        {
            var s = Perfect();
            s.RedLightsRun = 10;
            s.Stalls = 10;
            s.Collisions = 10;
            var r = MissionScoring.Compute(s, 0);
            Assert.GreaterOrEqual(r.scoreSignals, 0);
            Assert.GreaterOrEqual(r.scoreTechnique, 0);
            Assert.GreaterOrEqual(r.scoreDefensive, 0);
            Assert.GreaterOrEqual(r.scoreVehicle, 0);
        }

        [Test]
        public void CalarElMotor_CastigaLaTecnica_YSeLlevaElBonoDeLimpieza()
        {
            var s = Perfect();
            s.Stalls = 2;
            var r = MissionScoring.Compute(s, 0);
            // Base 12 (sin bono, ya no fue limpia) − 2×4.
            Assert.AreEqual(4, r.scoreTechnique);
        }

        [Test]
        public void GirarSinDireccional_CastigaLaDefensiva()
        {
            // El GDD pide penalizar la omisión de direccionales.
            var s = Perfect();
            s.MissedBlinkers = 2;
            var r = MissionScoring.Compute(s, 0);
            // Base 9 (sin bono) − 2×2.
            Assert.AreEqual(5, r.scoreDefensive);
        }

        // ---- La exigencia pedida en el playtest 2026-07-25 ----

        [Test]
        public void UnaCorridaDeNovato_YaNoAprueba()
        {
            // El caso que motivó el cambio: se llegaba con errores de sobra y
            // el juego regalaba el aprobado. Dos calados, un rechinido, un
            // roce, un giro sin avisar y un rojo NO es para pasar un examen.
            var s = Perfect();
            s.Stalls = 2; s.Grinds = 1; s.Collisions = 1;
            s.MissedBlinkers = 1; s.RedLightsRun = 1;
            var r = MissionScoring.Compute(s, 0);
            Assert.Less(r.totalScore, 70,
                $"una corrida así debe reprobar y dio {r.totalScore}");
        }

        [Test]
        public void ManejarLimpio_SigueDando100()
        {
            // La exigencia sube, pero el techo no se mueve: quien maneja bien
            // saca 100. Si no, el jugador no tendría meta a la que aspirar.
            var r = MissionScoring.Compute(Perfect(), 0);
            Assert.AreEqual(100, r.totalScore);
        }

        [Test]
        public void ElTiempoYaNoEsGratis_PasearseCuestaPuntos()
        {
            // Antes, cualquier llegada dentro del límite daba los 10 puntos.
            var justo = Perfect();
            justo.TimeUsed = justo.TimeLimit * 0.98f;
            var holgado = Perfect();
            holgado.TimeUsed = holgado.TimeLimit * 0.4f;

            Assert.AreEqual(10, MissionScoring.Compute(holgado, 0).scoreTime);
            Assert.Less(MissionScoring.Compute(justo, 0).scoreTime, 10,
                "llegar con la hora encima no puede valer lo mismo que llegar holgado");
        }

        [Test]
        public void DireccionalesYChoques_NoDejanDefensivaNegativa()
        {
            var s = Perfect();
            s.Collisions = 3;
            s.MissedBlinkers = 5;
            var r = MissionScoring.Compute(s, 0);
            Assert.AreEqual(0, r.scoreDefensive);
        }

        [Test]
        public void ExcederElTiempo_DegradaElPuntajeDeTiempo()
        {
            var s = Perfect();
            s.TimeUsed = s.TimeLimit + 40f; // 40 s tarde
            var r = MissionScoring.Compute(s, 0);
            Assert.Less(r.scoreTime, 10);
            Assert.GreaterOrEqual(r.scoreTime, 0);
        }

        [Test]
        public void ElDesglose_SiempreSumaElTotal()
        {
            var s = Perfect();
            s.Stalls = 1; s.RedLightsRun = 1; s.Collisions = 1;
            var r = MissionScoring.Compute(s, 0);
            Assert.AreEqual(
                r.scoreObjective + r.scoreSignals + r.scoreTechnique +
                r.scoreDefensive + r.scoreTime + r.scoreVehicle,
                r.totalScore);
        }
    }

    /// <summary>El catálogo alimenta el mapa, el briefing y los reintentos.</summary>
    public class MissionCatalogTests
    {
        [Test]
        public void HayOchoMisiones_LasSieteDelGddMasElBonus()
        {
            // T1, T2 (tutorial) + C1..C4 (conducción) + F1 (examen) + B1
            // "Quito entero" (bonus post-examen: la ciudad completa).
            Assert.AreEqual(8, MissionCatalog.All.Length);
        }

        [Test]
        public void LosIds_SonSecuenciales_YEntranEnElGuardado()
        {
            for (int i = 0; i < MissionCatalog.All.Length; i++)
                Assert.AreEqual(i, MissionCatalog.All[i].Id, "id fuera de orden");
            Assert.LessOrEqual(MissionCatalog.All.Length, Core.GameData.MaxMissions,
                "GameData no alcanza a guardar todas las misiones");
        }

        [Test]
        public void CadaMision_TieneEscenaTiempoYBriefing()
        {
            foreach (var m in MissionCatalog.All)
            {
                Assert.IsNotEmpty(m.SceneName, m.Title);
                Assert.Greater(m.TimeLimit, 0f, m.Title);
                Assert.IsNotEmpty(m.Briefing.donPanchoLine, m.Title);
                Assert.IsNotEmpty(m.Briefing.objectives, m.Title);
            }
        }

        [Test]
        public void SoloElExamenEsFinal_YElBonusVieneDespues()
        {
            int finales = 0;
            foreach (var m in MissionCatalog.All)
                if (m.IsFinal) finales++;
            Assert.AreEqual(1, finales, "debe haber UN examen final");

            // El examen cierra el GDD (id 6); el bonus de la ciudad completa
            // va después y se desbloquea al aprobarlo.
            Assert.AreEqual(6, MissionCatalog.Final.Id);
            Assert.AreEqual("N2_QuitoCiudad",
                MissionCatalog.All[MissionCatalog.All.Length - 1].SceneName);
            Assert.IsFalse(MissionCatalog.All[MissionCatalog.All.Length - 1].IsFinal,
                "el bonus no usa las pantallas de cierre narrativo");
        }

        [Test]
        public void ForScene_EncuentraLaPrimeraMisionDeCadaZona()
        {
            // El barrio aloja los dos tutoriales y la cuesta; la ciudad grande
            // empieza en "El redondel" (id 3).
            Assert.AreEqual(0, MissionCatalog.ForScene("N1_ZonaSur").Id);
            Assert.AreEqual(3, MissionCatalog.ForScene("N1_CiudadToon").Id);
            Assert.IsNull(MissionCatalog.ForScene("N0_TestDrive"), "manejo libre");
        }

        [Test]
        public void ForSceneConPreferida_LaPreferidaManda_SiViveEnLaEscena()
        {
            // Varias misiones comparten la ciudad grande: el id activo decide.
            Assert.AreEqual(5, MissionCatalog.ForScene("N1_CiudadToon", 5).Id);
            // Preferida de OTRA zona: cae a la primera de la escena.
            Assert.AreEqual(3, MissionCatalog.ForScene("N1_CiudadToon", 0).Id);
            // Preferida inválida: también.
            Assert.AreEqual(3, MissionCatalog.ForScene("N1_CiudadToon", -1).Id);
            // Y en el barrio manda el tutorial que toque.
            Assert.AreEqual(1, MissionCatalog.ForScene("N1_ZonaSur", 1).Id);
        }

        [Test]
        public void LasMisionesDeUnaMismaZona_SeDistinguenPorSuMeta()
        {
            // Varias misiones comparten escena (2 en el barrio, 5 en la ciudad):
            // la META, el ancla o la densidad deben diferenciarlas — si no,
            // serían la misma misión repetida con otro nombre.
            foreach (var escena in new[] { "N1_ZonaSur", "N1_CiudadToon" })
            {
                var enEscena = new System.Collections.Generic.List<MissionDef>();
                foreach (var m in MissionCatalog.All)
                    if (m.SceneName == escena) enEscena.Add(m);
                Assert.GreaterOrEqual(enEscena.Count, 2, $"{escena} aloja varias misiones");

                for (int i = 0; i < enEscena.Count; i++)
                    for (int j = i + 1; j < enEscena.Count; j++)
                        Assert.IsTrue(enEscena[i].Goal != enEscena[j].Goal ||
                                      enEscena[i].GoalAnchor != enEscena[j].GoalAnchor ||
                                      enEscena[i].TrafficDensity != enEscena[j].TrafficDensity,
                            $"{enEscena[i].Title} y {enEscena[j].Title} son indistinguibles");
            }
        }

        [Test]
        public void BloquearElPasoCebra_Descuenta3EnSenales()
        {
            var s = new MissionStats
            {
                ReachedGoal = true,
                TimeUsed = 100f,
                TimeLimit = 240f,
                CrosswalkBlocks = 2,
            };
            var r = MissionScoring.Compute(s, 0);
            Assert.AreEqual(14, r.scoreSignals, "−3 por cada cebra bloqueada");
            Assert.AreEqual(94, r.totalScore);
        }

        [Test]
        public void MetasPorAncla_SiempreTienenNombre_YSonUnicasPorMision()
        {
            // Si una misión dice "mi meta es un ancla" pero no dice cuál, el
            // runner caería al modo por defecto y la misión sería otra.
            var vistos = new System.Collections.Generic.HashSet<string>();
            foreach (var m in MissionCatalog.All)
            {
                if (m.Goal != GoalMode.NamedAnchor) continue;
                Assert.IsFalse(string.IsNullOrEmpty(m.GoalAnchor),
                    $"'{m.Title}' usa NamedAnchor sin GoalAnchor");
                Assert.IsTrue(vistos.Add(m.GoalAnchor),
                    $"el ancla '{m.GoalAnchor}' está repetida: dos misiones tendrían la misma ruta");
            }
        }

        [Test]
        public void CadaNivelSeJuegaDondeCumpleSuPromesa()
        {
            // La regla que rige el reparto de escenas (playtest 2026-07-25):
            // cada nivel va DONDE su tema existe de verdad, no donde el mapa
            // sea más grande. Se descubrió por las malas — la cuesta en la
            // ciudad Toon mandaba al jugador por la zona industrial de la
            // autopista elevada, que es el único relieve que hay allí.
            Assert.AreEqual("N1_ZonaSur", MissionCatalog.Get(0).SceneName, "tutorial: el barrio");
            Assert.AreEqual("N1_ZonaSur", MissionCatalog.Get(1).SceneName, "tutorial: el barrio");
            Assert.AreEqual("N1_ZonaSur", MissionCatalog.Get(2).SceneName,
                "la cuesta se juega donde hay una cuesta construida (Guamaní)");
            Assert.AreEqual("N1_SimonBolivar", MissionCatalog.Get(4).SceneName,
                "la nocturna se juega en la avenida, que es una carretera de verdad");

            // Y los que SÍ piden ciudad grande la tienen.
            foreach (int id in new[] { 3, 5, 6 })
                Assert.AreEqual("N1_CiudadToon", MissionCatalog.Get(id).SceneName,
                    $"'{MissionCatalog.Get(id).Title}' necesita la ciudad grande");
            Assert.AreEqual("N2_QuitoCiudad", MissionCatalog.Get(7).SceneName, "el bonus");
        }

        [Test]
        public void LaSimonNoSeOscureceDosVeces()
        {
            // Esa escena ya nace de noche: aplicarle CityMood sería redundante.
            Assert.IsFalse(MissionCatalog.Get(4).NightMood);
        }

        [Test]
        public void DensidadesEquilibradas_NiEstorbo_NiAburrimiento()
        {
            // Balance del playtest 2026-07-14: había niveles con NPCs de sobra
            // (18 en Hora pico saturaba). Invariantes de la curva de tráfico:
            var porId = MissionCatalog.All;
            Assert.AreEqual(0, porId[0].TrafficDensity, "T1 es tutorial EN CALMA");
            foreach (var m in MissionCatalog.All)
                Assert.LessOrEqual(m.TrafficDensity, 12,
                    $"'{m.Title}': más de 12 NPCs estorba en vez de retar");
            // "Hora pico" (C4) es y debe seguir siendo la más densa del juego.
            foreach (var m in MissionCatalog.All)
                Assert.LessOrEqual(m.TrafficDensity, porId[5].TrafficDensity,
                    $"'{m.Title}' no puede superar a Hora pico");
            // El examen exige, pero limpio: menos tráfico que Hora pico.
            Assert.Less(MissionCatalog.Final.TrafficDensity, porId[5].TrafficDensity);
        }

        [Test]
        public void MismaRutaConMasTrafico_NuncaDaMenosTiempo()
        {
            // Regla de balance: el timer en 0 es fallo DURO (MissionRunner), y
            // esperar semáforos con tráfico consume tiempo. Si dos misiones
            // comparten escena y meta, la de más tráfico debe dar al menos el
            // mismo tiempo — el desafío es la convivencia vial, no el reloj.
            // "Misma ruta" = misma escena, mismo modo de meta Y la misma meta
            // concreta: en la ciudad grande varias misiones comparten escena
            // pero recorren rutas distintas, y esas no son comparables.
            foreach (var a in MissionCatalog.All)
                foreach (var b in MissionCatalog.All)
                    if (a.SceneName == b.SceneName && a.Goal == b.Goal &&
                        a.GoalAnchor == b.GoalAnchor &&
                        b.TrafficDensity > a.TrafficDensity && !b.IsFinal)
                        Assert.GreaterOrEqual(b.TimeLimit, a.TimeLimit,
                            $"'{b.Title}' tiene más tráfico que '{a.Title}' pero menos tiempo");
        }
    }
}
