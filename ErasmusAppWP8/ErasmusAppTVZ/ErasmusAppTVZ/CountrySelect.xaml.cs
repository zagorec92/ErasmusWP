﻿using ErasmusAppTVZ.Helpers;
using ErasmusAppTVZ.Resources;
using ErasmusAppTVZ.ViewModel.City;
using ErasmusAppTVZ.ViewModel.Country;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Shell;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace ErasmusAppTVZ
{
    public partial class CountrySelect : PhoneApplicationPage
    {
        //constant for map zoom level
        private const double ZOOM_LEVEL = 5.5;

        //helpers for preserving and controlling elements state
        private static bool hasCoordinates = false;
        private static bool isFirstNavigation = true;
        private static bool isMapVisible = false;
        private static string currentlyOpenedExpander = null;

        //variable for storing country code by ISO 3166-1 standard
        //private static string countryCode;

        //helper for deciding which sort parameter is used
        private static int sortCounter = 0;

        //array for storing latitude and longitude
        private double[] countryCoordinates;

        private static CountryModel model;

        //private int selectedCountryIndex;
        //MainPage mp = new MainPage();

        /// <summary>
        /// Constructor
        /// </summary>
        public CountrySelect()
        {
            InitializeComponent();

            //loadLoginProperties();

            BuildLocalizedApplicationBar();
        }

        //protected void loadLoginProperties()
        //{
        //    using (IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream("login.txt", FileMode.Open, mp.isf))
        //    {
        //        using (StreamReader reader = new StreamReader(isoStream))
        //        {
        //            selectedCountryIndex = int.Parse(reader.ReadToEnd()) + 1;
        //        }
        //    }
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            model.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            if (IsolatedStorageSettings.ApplicationSettings.Contains("preferences"))
                Application.Current.Terminate();
        }

        /// <summary>
        /// Checks if 'search' parameter exists
        /// If parameter does exists, get the filtered results and pass it to DataContext
        /// If parameter does not exists, do nothing
        /// </summary>
        /// <param name="e"></param>
        /// <summary>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            listBox.Opacity = 0;

            //If this is initial call to the event
            if (isFirstNavigation)
            {
                SystemTray.ProgressIndicator = new ProgressIndicator();
                ProgressIndicatorHelper.SetProgressBar(true, AppResources.ProgressIndicatorCountries);

                //Get index of previously selected country
                int selectedCountryIndex = Int32.Parse(IsolatedStorageSettings.
                    ApplicationSettings["selectedCountryIndex"].ToString());
                
                //Populate CountryModel with every CountryData that satisfies parameters 
                model = new CountryModel()
                {
                    Countries = await App.MobileService.GetTable<CountryData>().
                        Where(x => x.Id != selectedCountryIndex).
                        ToListAsync()
                };

                
                Random rand = new Random();

                //Convert Flag to FlagImage
                //After conversion, empty Flag property
                foreach (CountryData data in model.Countries)
                {
                    if(data.Rating == 0.0)
                        data.Rating = rand.Next(0, 5);

                    data.FlagImage = ImageConversionHelper.ToImage(data.Flag);
                    data.Flag = String.Empty;
                }

                //initialize double array for country coordinates
                countryCoordinates = new double[2];

                ProgressIndicatorHelper.SetProgressBar(false, null);
                isFirstNavigation = false;
            }

            textBoxSearch.Text = String.Empty;
            textBoxSearch.Visibility = System.Windows.Visibility.Collapsed;

            //If user has entered a search term
            if (NavigationContext.QueryString.ContainsKey("search"))
            {
                sortCounter = 0;

                string searchTerm = NavigationContext.QueryString["search"];

                //Populate CountryModel with data that satisfies search term
                //Always search entire model, not DataContext
                CountryModel filteredModel = new CountryModel() 
                {
                    Countries = model.Countries.Where(x => x.Name.Contains(searchTerm)).ToList()
                };
                //filteredModel.Countries = model.Countries.Where(x => x.Name.Contains(searchTerm)).ToList();

                DataContext = filteredModel;

                //Preserve map visibility across navigation/refresh
                if (isMapVisible)
                    map.Visibility = System.Windows.Visibility.Visible;
            }
            else
                DataContext = model;

            //Animate listBox with countries
            AnimationHelper.Fade(listBox, 1, 900, new PropertyPath(OpacityProperty));
        }

        /// <summary>
        /// Build a localized application bar with icons and menu items
        /// </summary>
        private void BuildLocalizedApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            //Icon buttons
            ApplicationBarIconButton searchIconButton = new ApplicationBarIconButton();
            ApplicationBarIconButton showMapIconButton = new ApplicationBarIconButton();
            ApplicationBarIconButton sortIconButton = new ApplicationBarIconButton();

            searchIconButton.Text = AppResources.ApplicationBarSearch;
            showMapIconButton.Text = AppResources.ApplicationBarShowMap;
            sortIconButton.Text = AppResources.ApplicationBarSort;

            searchIconButton.IconUri = new Uri("/Assets/AppBar/search.png", UriKind.Relative);
            showMapIconButton.IconUri = new Uri("/Assets/AppBar/map.png", UriKind.Relative);
            sortIconButton.IconUri = new Uri("/Assets/AppBar/sort.png", UriKind.Relative);

            searchIconButton.Click += searchIconButton_Click;
            showMapIconButton.Click += showMapIconButton_Click;
            sortIconButton.Click += sortIconButton_Click;

            //Menu items
            ApplicationBarMenuItem profileMenuItem = new ApplicationBarMenuItem();
            ApplicationBarMenuItem optionsMenuItem = new ApplicationBarMenuItem();
            ApplicationBarMenuItem aboutMenuItem = new ApplicationBarMenuItem();

            profileMenuItem.Text = AppResources.ApplicationBarProfileMenuItem;
            optionsMenuItem.Text = AppResources.ApplicationBarOptionsMenuItem;
            aboutMenuItem.Text = AppResources.ApplicationBarAboutMenuItem;

            profileMenuItem.Click += profileMenuItem_Click;
            optionsMenuItem.Click += optionsMenuItem_Click;
            aboutMenuItem.Click += aboutMenuItem_Click;

            ApplicationBar.Buttons.Add(searchIconButton);
            ApplicationBar.Buttons.Add(showMapIconButton);
            ApplicationBar.Buttons.Add(sortIconButton);

            ApplicationBar.MenuItems.Add(profileMenuItem);
            ApplicationBar.MenuItems.Add(optionsMenuItem);
            ApplicationBar.MenuItems.Add(aboutMenuItem);

            ApplicationBar.IsVisible = true;
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetMapCenter()
        {
            map.Center = new GeoCoordinate(countryCoordinates[0], countryCoordinates[1]);
            map.ZoomLevel = ZOOM_LEVEL;
        }

        #region EventHandlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void profileMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not implemented");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void optionsMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not implemented");
        }

        /// <summary>
        /// Gets the countryCode for determining country latitude and longitude
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExpanderView_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ExpanderView ev = sender as ExpanderView;

            if (currentlyOpenedExpander != ev.Tag.ToString())
            {
                //if (ev.IsExpanded)
                //{
                hasCoordinates = false;
                int id = Int32.Parse(ev.Tag.ToString());

                GeocodeQuery query = new GeocodeQuery()
                {
                    GeoCoordinate = new System.Device.Location.GeoCoordinate(0, 0),
                    SearchTerm = (DataContext as CountryModel).Countries.Single(x => x.Id == id).Name
                };

                query.QueryCompleted += query_QueryCompleted;
                query.QueryAsync();

                currentlyOpenedExpander = ev.Tag.ToString();
                //}
            }

            //Execute only if expander is opened
            //if (ev.IsExpanded)
            //{
            //    hasCoordinates = false;
            //    int id = Int32.Parse(ev.Tag.ToString());

            //    GeocodeQuery query = new GeocodeQuery()
            //    {
            //        GeoCoordinate = new System.Device.Location.GeoCoordinate(0, 0),
            //        SearchTerm = (DataContext as CountryModel).Countries.Single(x => x.Id == id).Name
            //    };

            //    query.QueryCompleted += query_QueryCompleted;
            //    query.QueryAsync();
            //}
        }

        /// <summary>
        /// Gets the latitude and longitude and centers the map accordingly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void query_QueryCompleted(object sender, QueryCompletedEventArgs<IList<MapLocation>> e)
        {
            //defensive programming, trust no one
            if (e.Error == null)
            {
                countryCoordinates[0] = e.Result[0].GeoCoordinate.Latitude;
                countryCoordinates[1] = e.Result[0].GeoCoordinate.Longitude;

                if (map.Visibility == System.Windows.Visibility.Visible)
                    SetMapCenter();

                hasCoordinates = true;
            }
        }

        /// <summary>
        /// Waits until country coordinates are populated if double tap event handler is invoked
        /// </summary>
        /// <returns></returns>
        //private Task Wait()
        //{
        //    return Task.Run(() =>
        //    {
        //        while(true)
        //            if(hasCoordinates == true)
        //                return;
        //    });
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private /*async*/ void ExpanderView_DoubleTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ExpanderView ev = sender as ExpanderView;

            //Expander Tap event is also invoked, so waiting is needed until Inception passes
            //We should not wait long as it is only one layer in
            //No timeout, so theoretically, we could be stuck in Limbo (I'm lying)
            //await Task.Run(() =>
            //    {
            //        while (true)
            //            if (hasCoordinates)
            //                return;
            //    });

            //NavigationService.Navigate(new Uri(string.Format("/CitySelect.xaml?countryId={0}&mapVisible={1}&lat={2}&lon={3}",
            //    ev.Tag, isMapVisible, countryCoordinates[0].ToString(), countryCoordinates[1].ToString()), UriKind.Relative));
            NavigationService.Navigate(new Uri(string.Format("/CitySelect.xaml?countryId={0}&mapVisible={1}",
                ev.Tag, isMapVisible), UriKind.Relative));
        }

        /// <summary>
        /// Gets the CustomMessageBox with content
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            CustomMessageBox aboutMessageBox = ContentHelper.GetAboutMessageBox();
            aboutMessageBox.Show();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sortIconButton_Click(object sender, EventArgs e)
        {
            if (sortCounter == 0)
                DataContext = new CountryModel() { Countries = (DataContext as CountryModel).
                    Countries.OrderByDescending(x => x.Rating).ToList() };
            else if (sortCounter == 1)
                DataContext = new CountryModel() { Countries = (DataContext as CountryModel).
                    Countries.OrderByDescending(x => x.Name).ToList() };
            else
                DataContext = new CountryModel() { Countries = (DataContext as CountryModel).
                    Countries.OrderBy(x => x.Name).ToList() };

            sortCounter += 1;

            if (sortCounter == 3)
                sortCounter = 0;
        }

        /// <summary>
        /// Show or hide map and change the text of IconButton appropriately
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void showMapIconButton_Click(object sender, EventArgs e)
        {
            if (map.Visibility == System.Windows.Visibility.Visible)
            {
                map.Visibility = System.Windows.Visibility.Collapsed;
                (sender as ApplicationBarIconButton).Text = AppResources.ApplicationBarShowMap;
                isMapVisible = false;
                return;
            }

            (sender as ApplicationBarIconButton).Text = AppResources.ApplicationBarHideMap;
            map.Visibility = System.Windows.Visibility.Visible;
            isMapVisible = true;

            if (hasCoordinates)
                SetMapCenter();
            //if(countryCode != null)
            //    CoordinatesHelper.SetMapCenter(ref map, await CoordinatesHelper.GetCoordinates(countryCode, 1), ZOOM_LEVEL);
        }

        /// <summary>
        /// Check if textBoxSearch.Text.Length is greater than 0
        /// If yes, than refresh page with search parameter
        /// If not, show TextBox and get focus
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void searchIconButton_Click(object sender, EventArgs e)
        {
            if (textBoxSearch.Text.Length == 0)
            {
                textBoxSearch.Visibility = System.Windows.Visibility.Visible;
                textBoxSearch.Focus();
            }
            else
                NavigationService.Navigate(new Uri(string.Format("/CountrySelect.xaml" +
                                    "?Refresh=true&search={0}", textBoxSearch.Text), UriKind.Relative));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button bttn = sender as Button;

            NavigationService.Navigate(new Uri(string.Format("/CitySelect.xaml?countryId={0}&mapVisible={1}&lat={2}&lon={3}",
                bttn.Tag, isMapVisible, countryCoordinates[0], countryCoordinates[1]), UriKind.Relative));
        }

        /// <summary>
        /// Increments map zoom level by 1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //private void ButtonZoomIn_Click(object sender, RoutedEventArgs e)
        //{
        //    map.ZoomLevel += 1;
        //}

        /// <summary>
        /// Decrements map zoom level by 1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //private void ButtonZoomOut_Click(object sender, RoutedEventArgs e)
        //{
        //    map.ZoomLevel -= 1;
        //}
        #endregion


    }//class
}//namespace