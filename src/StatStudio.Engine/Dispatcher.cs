namespace StatStudio.Engine;

/// <summary>
/// Routes a request <c>op</c> to its handler. Each handler mirrors the corresponding WPF
/// <c>MainWindow.On*</c> method, with the dialog replaced by JSON <c>params</c> and the
/// output returned instead of appended. Handlers live in the <c>Ops*</c> classes.
/// </summary>
internal static class Dispatcher
{
    public static EngineResponse Handle(EngineRequest req)
    {
        var res = new EngineResponse { Id = req.Id, Ok = true };
        try
        {
            switch (req.Op)
            {
                case "ping": res.SessionText = "pong"; break;

                // ---- File / data ----
                case "import": OpsData.Import(req, res); break;
                case "export": OpsData.Export(req, res); break;
                case "project.load": OpsData.ProjectLoad(req, res); break;
                case "project.save": OpsData.ProjectSave(req, res); break;
                case "samples.list": OpsData.SamplesList(req, res); break;
                case "samples.load": OpsData.SamplesLoad(req, res); break;
                case "calc.evaluate": OpsData.Calc(req, res); break;

                // ---- Basic statistics ----
                case "descriptives": OpsBasic.Descriptives(req, res); break;
                case "ttest.one": OpsBasic.OneSampleT(req, res); break;
                case "ttest.two": OpsBasic.TwoSampleT(req, res); break;
                case "ttest.paired": OpsBasic.PairedT(req, res); break;
                case "prop.one": OpsBasic.OneProportion(req, res); break;
                case "prop.two": OpsBasic.TwoProportions(req, res); break;
                case "chisq.gof": OpsBasic.ChiSquareGof(req, res); break;
                case "chisq.assoc": OpsBasic.ChiSquareAssoc(req, res); break;
                case "corr.pearson": OpsBasic.Correlation(req, res, spearman: false); break;
                case "corr.spearman": OpsBasic.Correlation(req, res, spearman: true); break;
                case "normality": OpsBasic.Normality(req, res); break;
                case "var.two": OpsBasic.TwoVariances(req, res); break;
                case "fisher": OpsBasic.Fisher(req, res); break;

                // ---- ANOVA ----
                case "anova.oneway": OpsAnova.OneWay(req, res); break;
                case "anova.twoway": OpsAnova.TwoWay(req, res); break;
                case "var.equal": OpsAnova.EqualVariances(req, res); break;

                // ---- Nonparametrics ----
                case "np.mannwhitney": OpsNonparam.MannWhitney(req, res); break;
                case "np.wilcoxon": OpsNonparam.Wilcoxon(req, res); break;
                case "np.kruskal": OpsNonparam.KruskalWallis(req, res); break;
                case "np.sign": OpsNonparam.SignTest(req, res); break;
                case "np.runs": OpsNonparam.RunsTest(req, res); break;

                // ---- Regression ----
                case "reg.simple": OpsRegression.Simple(req, res); break;
                case "reg.multiple": OpsRegression.Multiple(req, res); break;
                case "reg.poly": OpsRegression.Polynomial(req, res); break;
                case "reg.bestsubsets": OpsRegression.BestSubsets(req, res); break;
                case "reg.stepwise": OpsRegression.Stepwise(req, res); break;
                case "reg.logistic": OpsRegression.Logistic(req, res); break;

                // ---- Time series ----
                case "ts.trend": OpsTimeSeries.Trend(req, res); break;
                case "ts.movavg": OpsTimeSeries.MovingAverage(req, res); break;
                case "ts.singleexp": OpsTimeSeries.SingleExp(req, res); break;
                case "ts.doubleexp": OpsTimeSeries.DoubleExp(req, res); break;
                case "ts.winters": OpsTimeSeries.Winters(req, res); break;
                case "ts.decomp": OpsTimeSeries.Decompose(req, res); break;
                case "ts.acf": OpsTimeSeries.Acf(req, res, partial: false); break;
                case "ts.pacf": OpsTimeSeries.Acf(req, res, partial: true); break;
                case "ts.arima": OpsTimeSeries.Arima(req, res); break;
                case "ts.sarima": OpsTimeSeries.Sarima(req, res); break;

                // ---- Multivariate / reliability / power / bayes / mixed ----
                case "mv.pca": OpsMultivariate.Pca(req, res); break;
                case "mv.factor": OpsMultivariate.Factor(req, res); break;
                case "mv.kmeans": OpsMultivariate.KMeans(req, res); break;
                case "rel.distfit": OpsMultivariate.DistFit(req, res); break;
                case "rel.kaplanmeier": OpsMultivariate.KaplanMeier(req, res); break;
                case "power": OpsMultivariate.PowerSampleSize(req, res); break;
                case "bayes.prop": OpsMultivariate.BayesProportion(req, res); break;
                case "bayes.normal": OpsMultivariate.BayesNormal(req, res); break;
                case "bayes.reg": OpsMultivariate.BayesRegression(req, res); break;
                case "mixed.onewayrandom": OpsMultivariate.OneWayRandom(req, res); break;

                // ---- SPC / quality ----
                case "spc.xbarr": OpsSpc.VariablesChart(req, res, useRange: true); break;
                case "spc.xbars": OpsSpc.VariablesChart(req, res, useRange: false); break;
                case "spc.imr": OpsSpc.Imr(req, res); break;
                case "spc.p": OpsSpc.Attribute(req, res, "P"); break;
                case "spc.np": OpsSpc.Attribute(req, res, "NP"); break;
                case "spc.c": OpsSpc.Attribute(req, res, "C"); break;
                case "spc.u": OpsSpc.Attribute(req, res, "U"); break;
                case "qual.capability": OpsSpc.Capability(req, res); break;
                case "qual.gagerr": OpsSpc.GageRR(req, res); break;

                // ---- DOE ----
                case "doe.factorial.create": OpsDoe.CreateFactorial(req, res); break;
                case "doe.fractional.create": OpsDoe.CreateFractional(req, res); break;
                case "doe.factorial.analyze": OpsDoe.AnalyzeFactorial(req, res); break;
                case "doe.rsm.create": OpsDoe.CreateRsm(req, res); break;
                case "doe.rsm.analyze": OpsDoe.AnalyzeRsm(req, res); break;
                case "doe.mixture.create": OpsDoe.CreateMixture(req, res); break;
                case "doe.mixture.analyze": OpsDoe.AnalyzeMixture(req, res); break;

                // ---- Graphs ----
                case "graph.histogram": OpsGraph.Histogram(req, res); break;
                case "graph.boxplot": OpsGraph.Boxplot(req, res); break;
                case "graph.scatter": OpsGraph.Scatter(req, res); break;
                case "graph.timeseries": OpsGraph.TimeSeries(req, res); break;
                case "graph.probplot": OpsGraph.ProbPlot(req, res); break;

                default:
                    res.Ok = false;
                    res.Error = $"unknown op '{req.Op}'";
                    break;
            }
        }
        catch (Exception ex)
        {
            // Handlers fill `res` incrementally, so a mid-flight throw can leave half an
            // analysis attached. Drop it — a failed op must not ship partial output.
            res.StatusTitle = null;
            res.SessionText = null;
            res.Worksheet = null;
            res.Graphs = null;
            res.Samples = null;
            res.Ok = false;
            res.Error = ex.Message;
        }
        return res;
    }
}
