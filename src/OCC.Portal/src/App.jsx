import { useState, useEffect } from 'react';
import './App.css';

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(
    localStorage.getItem('occ_portal_token') ? true : false
  );
  const [token, setToken] = useState(localStorage.getItem('occ_portal_token') || '');
  const [user, setUser] = useState(
    localStorage.getItem('occ_portal_user') ? JSON.parse(localStorage.getItem('occ_portal_user')) : null
  );
  
  // Responsive navigation state for mobile devices
  const [activeMobileSection, setActiveMobileSection] = useState('list'); // 'list' or 'details'

  // Connection settings
  const [apiUrl, setApiUrl] = useState(
    localStorage.getItem('occ_portal_api_url') || 'https://api.origize63.co.za'
  );

  // Login form state
  const [email, setEmail] = useState('neil@mdk.co.za');
  const [password, setPassword] = useState('pass');
  const [rememberMe, setRememberMe] = useState(true);
  const [loginError, setLoginError] = useState('');
  const [loginLoading, setLoginLoading] = useState(false);

  // Registration form state
  const [authMode, setAuthMode] = useState('login'); // 'login' or 'register'
  const [regFirstName, setRegFirstName] = useState('');
  const [regLastName, setRegLastName] = useState('');
  const [regEmail, setRegEmail] = useState('');
  const [regPassword, setRegPassword] = useState('');
  const [regConfirmPassword, setRegConfirmPassword] = useState('');
  const [regPhone, setRegPhone] = useState('');
  const [regCompanyName, setRegCompanyName] = useState('');
  const [regLocation, setRegLocation] = useState('');
  const [regError, setRegError] = useState('');
  const [regSuccess, setRegSuccess] = useState('');
  const [regLoading, setRegLoading] = useState(false);

  // Projects state
  const [projects, setProjects] = useState([]);
  const [selectedProjectId, setSelectedProjectId] = useState(null);
  const [projectDetails, setProjectDetails] = useState(null);
  const [projectsLoading, setProjectsLoading] = useState(false);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [projectsError, setProjectsError] = useState('');
  const [detailsError, setDetailsError] = useState('');

  // Live time clock for login overlay
  const [currentTime, setCurrentTime] = useState(new Date());

  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentTime(new Date());
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  // Fetch projects when authenticated
  useEffect(() => {
    if (isAuthenticated && token) {
      fetchProjects();
    }
  }, [isAuthenticated, token, apiUrl]);

  // Fetch detailed project information when project selected
  useEffect(() => {
    if (selectedProjectId) {
      fetchProjectDetails(selectedProjectId);
    } else {
      setProjectDetails(null);
    }
  }, [selectedProjectId]);

  const handleLogin = async (e) => {
    e.preventDefault();
    if (!email || !password) {
      setLoginError('Please enter both email and password.');
      return;
    }

    setLoginLoading(true);
    setLoginError('');

    try {
      localStorage.setItem('occ_portal_api_url', apiUrl);
      
      const response = await fetch(`${apiUrl}/api/Auth/login`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ email, password }),
      });

      if (!response.ok) {
        if (response.status === 403) {
          const errorMsg = await response.text();
          throw new Error(errorMsg || 'Account pending approval.');
        }
        throw new Error('Invalid email or password.');
      }

      const data = await response.json();
      
      if (rememberMe) {
        localStorage.setItem('occ_portal_token', data.token);
        localStorage.setItem('occ_portal_user', JSON.stringify(data.user));
      }

      setToken(data.token);
      setUser(data.user);
      setIsAuthenticated(true);
    } catch (err) {
      setLoginError(err.message || 'Failed to connect to the server.');
    } finally {
      setLoginLoading(false);
    }
  };

  const handleRegister = async (e) => {
    e.preventDefault();
    if (!regFirstName || !regLastName || !regEmail || !regPassword || !regConfirmPassword) {
      setRegError('Please fill in all required fields.');
      return;
    }

    if (regPassword !== regConfirmPassword) {
      setRegError('Passwords do not match.');
      return;
    }

    setRegLoading(true);
    setRegError('');
    setRegSuccess('');

    try {
      const response = await fetch(`${apiUrl}/api/Auth/register`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          firstName: regFirstName,
          lastName: regLastName,
          email: regEmail,
          password: regPassword,
          phone: regPhone || null,
          companyName: regCompanyName || null,
          location: regLocation || null,
        }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText || 'Registration failed.');
      }

      setRegSuccess('Registration submitted successfully! Please wait for admin approval.');
      // Clear fields
      setRegFirstName('');
      setRegLastName('');
      setRegEmail('');
      setRegPassword('');
      setRegConfirmPassword('');
      setRegPhone('');
      setRegCompanyName('');
      setRegLocation('');
      
      // Auto-switch to login tab after 3 seconds
      setTimeout(() => {
        setAuthMode('login');
        setRegSuccess('');
      }, 4000);
    } catch (err) {
      setRegError(err.message || 'An error occurred during registration.');
    } finally {
      setRegLoading(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('occ_portal_token');
    localStorage.removeItem('occ_portal_user');
    setToken('');
    setUser(null);
    setIsAuthenticated(false);
    setProjects([]);
    setProjectDetails(null);
    setSelectedProjectId(null);
    setActiveMobileSection('list');
  };

  const fetchProjects = async () => {
    setProjectsLoading(true);
    setProjectsError('');
    try {
      const response = await fetch(`${apiUrl}/api/ClientPortal/projects`, {
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      });

      if (!response.ok) {
        if (response.status === 401) {
          handleLogout();
          throw new Error('Session expired. Please log in again.');
        }
        throw new Error('Failed to retrieve projects.');
      }

      const data = await response.json();
      setProjects(data);
      if (data.length > 0 && !selectedProjectId) {
        setSelectedProjectId(data[0].id);
      }
    } catch (err) {
      setProjectsError(err.message || 'An error occurred fetching projects.');
    } finally {
      setProjectsLoading(false);
    }
  };

  const fetchProjectDetails = async (projectId) => {
    setDetailsLoading(true);
    setDetailsError('');
    try {
      const response = await fetch(`${apiUrl}/api/ClientPortal/projects/${projectId}`, {
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      });

      if (!response.ok) {
        throw new Error('Failed to load project details.');
      }

      const data = await response.json();
      setProjectDetails(data);
    } catch (err) {
      setDetailsError(err.message || 'Error loading details.');
    } finally {
      setDetailsLoading(false);
    }
  };

  // Helper formatting functions
  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-ZA', { year: 'numeric', month: 'short', day: 'numeric' });
  };

  const getStatusColor = (status) => {
    switch (status?.toLowerCase()) {
      case 'completed':
        return '#00E676';
      case 'in progress':
      case 'active':
        return '#FF9100';
      case 'planning':
      case 'not started':
      default:
        return '#90A4AE';
    }
  };

  // Calculate timelines relative positions for custom Gantt rendering
  const renderGanttTimeline = (project, tasks) => {
    if (!project || !tasks || tasks.length === 0) return null;

    const projStart = new Date(project.StartDate).getTime();
    const projEnd = new Date(project.EndDate).getTime();
    const projDuration = projEnd - projStart || 1;

    // Generate weekly markers
    const markers = [];
    const dateCursor = new Date(project.StartDate);
    // Align to Monday
    const day = dateCursor.getDay();
    const diff = dateCursor.getDate() - day + (day === 0 ? -6 : 1);
    dateCursor.setDate(diff);

    while (dateCursor.getTime() <= projEnd) {
      const markerTime = dateCursor.getTime();
      const leftPercent = ((markerTime - projStart) / projDuration) * 100;
      if (leftPercent >= 0 && leftPercent <= 100) {
        markers.push({
          label: dateCursor.toLocaleDateString('en-ZA', { month: 'short', day: 'numeric' }),
          left: leftPercent
        });
      }
      dateCursor.setDate(dateCursor.getDate() + 7); // Increment by 1 week
    }

    return (
      <div className="gantt-chart">
        <div className="gantt-header-row">
          <div className="gantt-task-name-header">Tasks & Timeline</div>
          <div className="gantt-timeline-header">
            {markers.map((m, idx) => (
              <span key={idx} className="gantt-marker-label" style={{ left: `${m.left}%` }}>
                {m.label}
              </span>
            ))}
          </div>
        </div>
        <div className="gantt-body">
          {markers.map((m, idx) => (
            <div key={`grid-${idx}`} className="gantt-grid-line" style={{ left: `${m.left}%` }}></div>
          ))}
          {tasks.map((task) => {
            const tStart = new Date(task.StartDate).getTime();
            const tEnd = new Date(task.FinishDate).getTime();
            
            // Calculate percentage positions bounded between 0% and 100%
            let leftPercent = ((tStart - projStart) / projDuration) * 100;
            let widthPercent = ((tEnd - tStart) / projDuration) * 100;

            if (leftPercent < 0) {
              widthPercent += leftPercent;
              leftPercent = 0;
            }
            if (leftPercent + widthPercent > 100) {
              widthPercent = 100 - leftPercent;
            }
            if (widthPercent <= 0) {
              widthPercent = 1.5; // Minimum visible width for milestone tasks
            }

            return (
              <div key={task.Id} className="gantt-row">
                <div className="gantt-task-info">
                  <div className="gantt-task-title">{task.Name}</div>
                  <div className="gantt-task-dates">
                    {new Date(task.StartDate).toLocaleDateString('en-ZA', { month: '2-digit', day: '2-digit' })} - {new Date(task.FinishDate).toLocaleDateString('en-ZA', { month: '2-digit', day: '2-digit' })}
                  </div>
                </div>
                <div className="gantt-timeline-track">
                  <div 
                    className="gantt-task-bar" 
                    style={{ 
                      left: `${leftPercent}%`, 
                      width: `${widthPercent}%`,
                      backgroundColor: getStatusColor(task.Status)
                    }}
                    title={`${task.Name}: ${task.Progress}% Complete (${task.Status})`}
                  >
                    <div className="gantt-bar-progress" style={{ width: `${task.Progress}%` }}></div>
                    {widthPercent > 10 && (
                      <span className="gantt-bar-label">{task.Progress}%</span>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    );
  };

  // --- LOGIN INTERFACE ---
  if (!isAuthenticated) {
    const formattedDate = currentTime.toLocaleDateString('en-ZA', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    });
    const formattedTime = currentTime.toLocaleTimeString('en-ZA', {
      hour12: false
    });

    return (
      <div className="login-container">
        {/* Fullscreen background overlay */}
        <div className="login-background"></div>
        <div className="login-backdrop-overlay"></div>

        {/* Left Side: Branded Future Theme Area */}
        <div className="login-logo-side">
          <div className="occ-logo-badge">
            <div className="occ-logo-glow"></div>
            <img src="occ_logo.png" alt="Orange Circle Construction" className="occ-logo-img" />
          </div>

          <div className="time-badge">
            <div className="time-badge-header">
              <span className="pulse-dot"></span>
              BUILDING IN PROGRESS
            </div>
            <div className="time-badge-date">{formattedDate}</div>
            <div className="time-badge-clock">{formattedTime}</div>
          </div>
        </div>

        {/* Right Side: Auth Card with Glassmorphism */}
        <div className="login-form-side">
          <div className="login-card">
            <div className="login-tabs">
              <span 
                className={`tab-item ${authMode === 'login' ? 'active' : ''}`}
                onClick={() => { setAuthMode('login'); setRegError(''); setRegSuccess(''); setLoginError(''); }}
              >
                Login
              </span>
              <span 
                className={`tab-item ${authMode === 'register' ? 'active' : ''}`}
                onClick={() => { setAuthMode('register'); setRegError(''); setRegSuccess(''); setLoginError(''); }}
              >
                Register
              </span>
            </div>

            {authMode === 'login' ? (
              <>
                <p className="login-subtitle">Welcome back! Please login to your account</p>

                <form onSubmit={handleLogin} className="login-form">
                  {loginError && <div className="error-alert">{loginError}</div>}

                  <div className="input-group">
                    <span className="input-icon">✉</span>
                    <input
                      type="email"
                      placeholder="name@company.co.za"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      required
                    />
                  </div>

                  <div className="input-group">
                    <span className="input-icon">🔒</span>
                    <input
                      type="password"
                      placeholder="Password"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      required
                    />
                  </div>

                  <div className="input-group api-url-group">
                    <span className="input-icon">🌐</span>
                    <input
                      type="text"
                      placeholder="Server API URL"
                      value={apiUrl}
                      onChange={(e) => setApiUrl(e.target.value)}
                      required
                    />
                    <span className="api-url-tip">API host server environment</span>
                  </div>

                  <div className="form-options">
                    <label className="remember-label">
                      <input
                        type="checkbox"
                        checked={rememberMe}
                        onChange={(e) => setRememberMe(e.target.checked)}
                      />
                      Remember Me
                    </label>
                    <span className="forgot-password">Forget Password?</span>
                  </div>

                  <button type="submit" className="btn-login" disabled={loginLoading}>
                    {loginLoading ? (
                      <span className="spinner">Connecting...</span>
                    ) : (
                      <>
                        <span className="btn-icon">🔒</span> Login
                      </>
                    )}
                  </button>

                  <div className="register-footer">
                    Don't have an account?{' '}
                    <span 
                      className="register-link" 
                      onClick={() => { setAuthMode('register'); setRegError(''); setRegSuccess(''); setLoginError(''); }}
                    >
                      Register
                    </span>
                  </div>
                </form>
              </>
            ) : (
              <>
                <p className="login-subtitle">Request access to the OCC Client Portal</p>

                <form onSubmit={handleRegister} className="login-form">
                  {regError && <div className="error-alert">{regError}</div>}
                  {regSuccess && <div className="success-alert">{regSuccess}</div>}

                  <div className="input-row">
                    <div className="input-group">
                      <span className="input-icon">👤</span>
                      <input
                        type="text"
                        placeholder="First Name *"
                        value={regFirstName}
                        onChange={(e) => setRegFirstName(e.target.value)}
                        required
                      />
                    </div>
                    <div className="input-group">
                      <span className="input-icon">👤</span>
                      <input
                        type="text"
                        placeholder="Last Name *"
                        value={regLastName}
                        onChange={(e) => setRegLastName(e.target.value)}
                        required
                      />
                    </div>
                  </div>

                  <div className="input-group">
                    <span className="input-icon">✉</span>
                    <input
                      type="email"
                      placeholder="Email Address *"
                      value={regEmail}
                      onChange={(e) => setRegEmail(e.target.value)}
                      required
                    />
                  </div>

                  <div className="input-row">
                    <div className="input-group">
                      <span className="input-icon">🔒</span>
                      <input
                        type="password"
                        placeholder="Password *"
                        value={regPassword}
                        onChange={(e) => setRegPassword(e.target.value)}
                        required
                      />
                    </div>
                    <div className="input-group">
                      <span className="input-icon">🔒</span>
                      <input
                        type="password"
                        placeholder="Confirm Password *"
                        value={regConfirmPassword}
                        onChange={(e) => setRegConfirmPassword(e.target.value)}
                        required
                      />
                    </div>
                  </div>

                  <div className="input-group">
                    <span className="input-icon">📞</span>
                    <input
                      type="tel"
                      placeholder="Phone Number (Optional)"
                      value={regPhone}
                      onChange={(e) => setRegPhone(e.target.value)}
                    />
                  </div>

                  <div className="input-row">
                    <div className="input-group">
                      <span className="input-icon">🏢</span>
                      <input
                        type="text"
                        placeholder="Company Name (Optional)"
                        value={regCompanyName}
                        onChange={(e) => setRegCompanyName(e.target.value)}
                      />
                    </div>
                    <div className="input-group">
                      <span className="input-icon">📍</span>
                      <input
                        type="text"
                        placeholder="Location/City (Optional)"
                        value={regLocation}
                        onChange={(e) => setRegLocation(e.target.value)}
                      />
                    </div>
                  </div>

                  <button type="submit" className="btn-login" disabled={regLoading}>
                    {regLoading ? (
                      <span className="spinner">Submitting...</span>
                    ) : (
                      <>
                        <span className="btn-icon">📝</span> Submit Request
                      </>
                    )}
                  </button>

                  <div className="register-footer">
                    Already have an account?{' '}
                    <span 
                      className="register-link" 
                      onClick={() => { setAuthMode('login'); setRegError(''); setRegSuccess(''); setLoginError(''); }}
                    >
                      Login
                    </span>
                  </div>
                </form>
              </>
            )}
          </div>
        </div>
      </div>
    );
  }

  // --- PORTAL DASHBOARD INTERFACE ---
  return (
    <div className="portal-container">
      {/* Top Navigation Bar */}
      <header className="portal-header">
        <div className="header-left">
          <img src="occ_logo.png" alt="OCC Logo" className="portal-logo" />
          <h1 className="header-title">ORANGE CIRCLE CONSTRUCTION</h1>
          <span className="portal-badge">CLIENT PORTAL</span>
        </div>
        <div className="header-right">
          <div className="user-profile">
            <span className="user-avatar">👤</span>
            <div className="user-details">
              <span className="user-name">{user?.FirstName} {user?.LastName}</span>
              <span className="user-email">{user?.Email}</span>
            </div>
          </div>
          <button className="btn-logout" onClick={handleLogout}>
            Logout ➔
          </button>
        </div>
      </header>

      {/* Main Dashboard Area */}
      <main className="portal-main">
        {/* Sidebar Project List */}
        <aside className={`projects-sidebar ${activeMobileSection === 'list' ? 'mobile-active' : 'mobile-hidden'}`}>
          <div className="sidebar-header">
            <h2>Your Projects</h2>
            <button className="btn-icon-reload" onClick={fetchProjects} title="Reload Projects" disabled={projectsLoading}>
              🔄
            </button>
          </div>

          {projectsLoading && (
            <div className="sidebar-state">
              <div className="simple-loader"></div>
              <span>Loading projects...</span>
            </div>
          )}

          {projectsError && (
            <div className="sidebar-state error-text">
              <p>{projectsError}</p>
              <button className="btn-retry" onClick={fetchProjects}>Retry</button>
            </div>
          )}

          {!projectsLoading && !projectsError && projects.length === 0 && (
            <div className="sidebar-state empty">
              No active projects linked to this account email.
            </div>
          )}

          <div className="projects-list">
            {projects.map((proj) => (
              <div
                key={proj.Id}
                className={`project-card ${selectedProjectId === proj.Id ? 'active' : ''}`}
                onClick={() => {
                  setSelectedProjectId(proj.Id);
                  setActiveMobileSection('details');
                }}
              >
                <div className="project-card-header">
                  <h3>{proj.Name}</h3>
                  <span
                    className="status-dot"
                    style={{ backgroundColor: getStatusColor(proj.Status) }}
                  ></span>
                </div>
                <p className="project-card-location">📍 {proj.Location || 'Site'}</p>
                <div className="project-card-progress">
                  <div className="progress-text">
                    <span>Progress</span>
                    <span>{proj.Progress}%</span>
                  </div>
                  <div className="progress-bar-container">
                    <div className="progress-bar-fill" style={{ width: `${proj.Progress}%` }}></div>
                  </div>
                </div>
                <div className="project-card-footer">
                  <span>Tasks: {proj.CompletedTasks}/{proj.TotalTasks}</span>
                  <span className="status-label" style={{ color: getStatusColor(proj.Status) }}>{proj.Status}</span>
                </div>
              </div>
            ))}
          </div>
        </aside>

        {/* Project Details Panel */}
        <section className={`project-details-container ${activeMobileSection === 'details' ? 'mobile-active' : 'mobile-hidden'}`}>
          {activeMobileSection === 'details' && (
            <div className="mobile-back-bar">
              <button 
                className="btn-back-mobile" 
                onClick={() => setActiveMobileSection('list')}
              >
                ← Back to Projects
              </button>
            </div>
          )}
          {detailsLoading && (
            <div className="details-state">
              <div className="simple-loader large"></div>
              <h3>Loading project details...</h3>
            </div>
          )}

          {detailsError && (
            <div className="details-state error">
              <h3>Failed to load project details</h3>
              <p>{detailsError}</p>
              <button className="btn-retry" onClick={() => fetchProjectDetails(selectedProjectId)}>Retry</button>
            </div>
          )}

          {!detailsLoading && !detailsError && !projectDetails && (
            <div className="details-state welcome">
              <div className="welcome-glow"></div>
              <h2>Welcome to your OCC Client Portal</h2>
              <p>Select a project from the side list to check task progress, site details, and timeline charts.</p>
            </div>
          )}

          {!detailsLoading && !detailsError && projectDetails && (
            <div className="project-details">
              {/* Meta Panel */}
              <div className="details-hero">
                <div className="hero-info">
                  <span className="hero-status-pill" style={{ borderColor: getStatusColor(projectDetails.Status), color: getStatusColor(projectDetails.Status) }}>
                    {projectDetails.Status}
                  </span>
                  <h2>{projectDetails.Name}</h2>
                  <p className="hero-desc">{projectDetails.Description || 'No project description available.'}</p>
                  <p className="hero-address">📍 {projectDetails.StreetLine1} {projectDetails.StreetLine2}, {projectDetails.City}, {projectDetails.Country}</p>
                </div>
                
                <div className="hero-stat-card">
                  <div className="stat-circle">
                    <svg viewBox="0 0 36 36" className="circular-chart">
                      <path className="circle-bg"
                        d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                      />
                      <path className="circle"
                        strokeDasharray={`${projectDetails.Progress}, 100`}
                        stroke={getStatusColor(projectDetails.Status)}
                        d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                      />
                      <text x="18" y="20.35" className="percentage">{projectDetails.Progress}%</text>
                    </svg>
                  </div>
                  <div className="stat-text">
                    <h4>Overall Progress</h4>
                    <p>Timeline Range:<br/>
                    <strong>{formatDate(projectDetails.StartDate)}</strong> to <strong>{formatDate(projectDetails.EndDate)}</strong></p>
                  </div>
                </div>
              </div>

              {/* Gantt Timeline View */}
              <div className="details-section">
                <h3>Visual Project Timeline (Gantt Chart)</h3>
                {projectDetails.Tasks && projectDetails.Tasks.length > 0 ? (
                  renderGanttTimeline(projectDetails, projectDetails.Tasks)
                ) : (
                  <div className="section-empty">No tasks defined for this project timeline yet.</div>
                )}
              </div>

              {/* Task Grid Table */}
              <div className="details-section">
                <h3>Detailed Task Progress</h3>
                {projectDetails.Tasks && projectDetails.Tasks.length > 0 ? (
                  <div className="tasks-table-wrapper">
                    <table className="tasks-table">
                      <thead>
                        <tr>
                          <th>Task Name</th>
                          <th>Start Date</th>
                          <th>End Date</th>
                          <th>Status</th>
                          <th style={{ textAlign: 'right' }}>Progress</th>
                        </tr>
                      </thead>
                      <tbody>
                        {projectDetails.Tasks.map((t) => (
                          <tr key={t.Id}>
                            <td>
                              <div className="task-title-cell">
                                <span className={`task-check ${t.IsComplete ? 'checked' : ''}`}>
                                  {t.IsComplete ? '✓' : '○'}
                                </span>
                                {t.Name}
                              </div>
                            </td>
                            <td>{formatDate(t.StartDate)}</td>
                            <td>{formatDate(t.FinishDate)}</td>
                            <td>
                              <span className="task-status-pill" style={{ backgroundColor: `${getStatusColor(t.Status)}20`, color: getStatusColor(t.Status) }}>
                                {t.Status}
                              </span>
                            </td>
                            <td style={{ textAlign: 'right', fontWeight: 'bold' }}>{t.Progress}%</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <div className="section-empty">No tasks found.</div>
                )}
              </div>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

export default App;
