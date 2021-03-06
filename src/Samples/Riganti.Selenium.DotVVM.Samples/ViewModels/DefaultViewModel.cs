using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotVVM.Framework.ViewModel;

namespace Riganti.Selenium.DotVVM.Samples.ViewModels
{
    public class DefaultViewModel : DotvvmViewModelBase
    {
        public string Title { get; set; }
        public List<RouteDTO> Routes { get; set; }

        public DefaultViewModel()
        {
            Title = "Hello from DotVVM!";
        }

        public override Task Load()
        {
            Routes = Context.Configuration.RouteTable.Where(a => a.RouteName != "DefaultRoute")
                .Select(s => new RouteDTO() {Name = s.RouteName, Url = s.Url}).ToList();
            return base.Load();
        }

        public async Task Wait()
        {
            await Task.Delay(2000);
            LabelText = "PostbackEnd";
        }

        public string LabelText { get; set; }
    }
}