import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { login, homeRouteForUser } from '../../auth';
import './LoginPage.css';
import { getOemAndFacilityTypes } from '../../services/All_Api_services';

export default function LoginPage(): React.JSX.Element {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const [platform, setPlatform] = useState<{ name: string; logo?: string; powered_by?: string } | null>(null);

  useEffect(() => {
    getOemAndFacilityTypes()
      .then((res) => {
        if (res?.success && res?.data?.platforms?.length > 0) {
          const firstPlatform = res.data.platforms[0];
          setPlatform({
            name: firstPlatform.name || '-',
            logo: firstPlatform.logo || undefined,
            powered_by: firstPlatform.powered_by || undefined
          });
        }
      })
      .catch((err) => {
        console.error('Failed to load platform settings', err);
      });
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setErrorMsg('');
    try {
      await login(email, password);
      // Land on the first page this role can see: Data Management for platform
      // staff, the application itself for a plant user.
      navigate(homeRouteForUser(), { replace: true });
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Login failed.');
      setIsLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-glass-panel">
        <div className="login-header">
          <div className="login-logo">
            {platform?.logo && (
              <img
                src={platform.logo}
                alt={platform.name}
                className="login-logo-img"
                style={{
                  height: '56px',
                  width: '56px',
                  objectFit: 'cover',
                  borderRadius: '8px',
                  boxShadow: '0 4px 12px rgba(0, 0, 0, 0.1)'
                }}
              />
            )}
          </div>
          <h1 className="login-title">{platform?.name || '-'}</h1>
          <p className="login-subtitle">Welcome back! Please enter your details.</p>
        </div>

        {errorMsg && (
          <div className="login-error" style={{ color: '#ef4444', backgroundColor: 'rgba(239, 68, 68, 0.1)', padding: '10px 12px', borderRadius: '8px', fontSize: '14px', marginBottom: '16px', border: '1px solid rgba(239, 68, 68, 0.2)' }}>
            {errorMsg}
          </div>
        )}

        <form className="login-form" onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="email">Email</label>
            <div className="input-wrapper">
              <span className="material-symbols-outlined input-icon">mail</span>
              <input
                id="email"
                type="email"
                placeholder="Enter your email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="password">Password</label>
            <div className="input-wrapper">
              <span className="material-symbols-outlined input-icon">lock</span>
              <input
                id="password"
                type="password"
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>
          </div>

          <div className="form-actions">
            <label className="remember-me">
              <input type="checkbox" />
              <span>Remember me</span>
            </label>
            {/* <a href="#" className="forgot-password">Forgot password?</a> */}
          </div>

          <button type="submit" className="login-btn" disabled={isLoading}>
            {isLoading ? (
              <span className="login-spinner"></span>
            ) : (
              'Sign in'
            )}
          </button>
        </form>

        <div className="login-footer">
          <p>{platform?.powered_by || '-'}</p>
        </div>
      </div>
      
      {/* Decorative background elements */}
      <div className="blob blob-1"></div>
      <div className="blob blob-2"></div>
    </div>
  );
}
